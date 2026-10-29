using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Alertas;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Alertas;

/// <summary>
/// Genera alertas a partir de una consulta ACTUAL de un usuario autenticado (D-02, RF-16).
/// No llama a OpenWeather: usa los valores ya validados y guardados en consulta_clima.
/// Reglas: RN-BD-20, 22, 23, 24, 25, 27, 35 y 42. Las alertas no se editan nunca (RN-BD-26).
/// </summary>
public sealed class MotorAlertas : IMotorAlertas
{
    /// <summary>D-09 / RN-BD-27: ventana anti-duplicado por usuario, ubicación y tipo.</summary>
    public static readonly TimeSpan VentanaAntiDuplicado = TimeSpan.FromMinutes(60);

    private readonly ClimaPlusDbContext _db;
    private readonly TimeProvider _reloj;
    private readonly ILogger<MotorAlertas> _log;

    public MotorAlertas(ClimaPlusDbContext db, TimeProvider reloj, ILogger<MotorAlertas> log)
    {
        _db = db;
        _reloj = reloj;
        _log = log;
    }

    /// <summary>
    /// true si una alerta anterior generada en <paramref name="anteriorUtc"/> impide otra en <paramref name="ahoraUtc"/>:
    /// bloquea mientras han pasado menos de 60 minutos; a los 60 exactos ya se permite (DP-8).
    /// La consulta SQL usa la misma comparación (fecha_generacion &gt; ahora - 60 min).
    /// </summary>
    public static bool BloqueaNuevaAlerta(DateTime anteriorUtc, DateTime ahoraUtc) => anteriorUtc > LimiteVentana(ahoraUtc);

    private static DateTime LimiteVentana(DateTime ahoraUtc) => ahoraUtc - VentanaAntiDuplicado;

    public async Task BloquearUsuarioAsync(long idUsuario, CancellationToken cancelacion)
    {
        if (!_db.Database.IsNpgsql())
        {
            // InMemory (pruebas) no tiene transacciones ni bloqueos; la concurrencia real se prueba con PostgreSQL.
            return;
        }

        if (_db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("El bloqueo del motor de alertas requiere una transacción abierta.");
        }

        // Bloqueo de transacción: se libera solo con COMMIT o ROLLBACK. Clave = id del usuario.
        await _db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({idUsuario})", cancelacion);
    }

    public async Task<IReadOnlyList<AlertaGeneradaResponse>> EvaluarConsultaActualAsync(ConsultaActualEvaluable consulta, CancellationToken cancelacion)
    {
        // Reglas activas del usuario (activo, RN-BD-22) para esta ubicación o globales. Máximo 6 filas.
        var reglas = await _db.ConfiguracionesAlerta
            .AsNoTracking()
            .Where(r => r.IdUsuario == consulta.IdUsuario
                        && r.Activa
                        && r.Usuario.Activo
                        && (r.IdUbicacion == consulta.IdUbicacion || r.IdUbicacion == null))
            .Select(r => new ReglaEvaluable(r.IdConfiguracion, r.Tipo, r.IdUbicacion, r.ValorUmbral, r.Activa))
            .ToListAsync(cancelacion);

        var propuestas = EvaluadorAlertas.Evaluar(
            consulta.Temperatura, consulta.CodigoCondicion, consulta.IdUbicacion, consulta.NombreUbicacion, reglas);
        if (propuestas.Count == 0)
        {
            return Array.Empty<AlertaGeneradaResponse>();
        }

        // Microsegundos: la misma precisión que guarda timestamptz, para que la respuesta y GET /api/alertas coincidan.
        var ahoraExacto = _reloj.GetUtcNow().UtcDateTime;
        var ahora = new DateTime(ahoraExacto.Ticks - (ahoraExacto.Ticks % 10), DateTimeKind.Utc);
        var limite = LimiteVentana(ahora);
        var tipos = propuestas.Select(p => p.Tipo).ToList();

        // RN-BD-27: cualquier alerta del mismo usuario, ubicación y tipo en la ventana, sin importar su estado ni origen.
        var bloqueados = await _db.Alertas
            .AsNoTracking()
            .Where(a => a.IdUsuario == consulta.IdUsuario
                        && a.IdUbicacion == consulta.IdUbicacion
                        && tipos.Contains(a.Tipo)
                        && a.FechaGeneracion > limite)
            .Select(a => a.Tipo)
            .Distinct()
            .ToListAsync(cancelacion);

        var nuevas = propuestas
            .Where(p => !bloqueados.Contains(p.Tipo))
            .Select(p => new Alerta
            {
                IdUsuario = consulta.IdUsuario,
                IdUbicacion = consulta.IdUbicacion,
                IdConsulta = consulta.IdConsulta,     // RN-BD-23: origen único, la consulta del mismo usuario (RN-BD-42)
                IdLectura = null,
                Tipo = p.Tipo,
                ValorDetectado = p.ValorDetectado,    // RN-BD-24: NULL en lluvia
                ValorUmbral = p.ValorUmbral,
                Mensaje = p.Mensaje,
                Estado = EstadosAlerta.Nueva,         // RN-BD-25
                FechaGeneracion = ahora,
            })
            .ToList();

        foreach (var tipo in bloqueados)
        {
            _log.LogInformation("Alertas: omitida_por_duplicado idUsuario={IdUsuario} idUbicacion={IdUbicacion} tipo={Tipo}",
                consulta.IdUsuario, consulta.IdUbicacion, tipo);
        }

        if (nuevas.Count == 0)
        {
            return Array.Empty<AlertaGeneradaResponse>();
        }

        _db.Alertas.AddRange(nuevas);
        await _db.SaveChangesAsync(cancelacion);

        foreach (var alerta in nuevas)
        {
            _db.Entry(alerta).State = EntityState.Detached;
            _log.LogInformation("Alertas: generada idAlerta={IdAlerta} idUsuario={IdUsuario} idUbicacion={IdUbicacion} idConsulta={IdConsulta} tipo={Tipo}",
                alerta.IdAlerta, alerta.IdUsuario, alerta.IdUbicacion, alerta.IdConsulta, alerta.Tipo);
        }

        return nuevas.Select(a => new AlertaGeneradaResponse(a.IdAlerta, a.Tipo, a.Mensaje, a.FechaGeneracion)).ToList();
    }
}
