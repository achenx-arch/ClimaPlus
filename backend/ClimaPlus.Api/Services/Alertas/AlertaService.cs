using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Alertas;
using ClimaPlus.Api.DTOs.Clima;
using ClimaPlus.Api.DTOs.Comun;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Alertas;

/// <summary>
/// Consulta y gestión de alertas generadas (M09 usuario, M15 administrador).
/// Solo lectura y cambio de estado: las alertas son historial (RN-BD-26) y las genera el
/// motor de alertas (MotorAlertas, Fase 2H) al consultar el clima actual con sesión. Sin alertas,
/// las listas salen vacías y el contador en cero.
/// </summary>
public sealed class AlertaService
{
    private readonly ClimaPlusDbContext _db;
    private readonly ILogger<AlertaService> _log;

    public AlertaService(ClimaPlusDbContext db, ILogger<AlertaService> log)
    {
        _db = db;
        _log = log;
    }

    // ------------------------------------------------------------------ Usuario (M09)

    public async Task<Resultado<PaginaResponse<AlertaResponse>>> ListarAsync(long idUsuario, FiltroAlertasRequest filtro, CancellationToken cancelacion)
    {
        var consulta = Filtrar(_db.Alertas.AsNoTracking().Where(a => a.IdUsuario == idUsuario), filtro);
        var total = await consulta.CountAsync(cancelacion);

        var elementos = await ProyectarUsuario(Paginar(consulta, filtro.Pagina)).ToListAsync(cancelacion);
        return Resultado<PaginaResponse<AlertaResponse>>.Ok(PaginaResponse<AlertaResponse>.Crear(elementos, filtro.Pagina, total));
    }

    public async Task<Resultado<ContadorAlertasResponse>> ContarNuevasAsync(long idUsuario, CancellationToken cancelacion)
    {
        var nuevas = await _db.Alertas.CountAsync(a => a.IdUsuario == idUsuario && a.Estado == EstadosAlerta.Nueva, cancelacion);
        return Resultado<ContadorAlertasResponse>.Ok(new ContadorAlertasResponse(nuevas));
    }

    /// <summary>
    /// Marca la alerta como LEIDA. Es idempotente: si ya estaba leída, responde igual.
    /// No hay vuelta a NUEVA (M09 solo ofrece "Marcar como leída").
    /// </summary>
    public async Task<Resultado<AlertaResponse>> MarcarLeidaAsync(long idUsuario, long idAlerta, CancellationToken cancelacion)
    {
        var alerta = await _db.Alertas.SingleOrDefaultAsync(a => a.IdAlerta == idAlerta && a.IdUsuario == idUsuario, cancelacion);
        if (alerta is null)
        {
            return Resultado<AlertaResponse>.Falla(new ErrorServicio(TipoError.NoEncontrado, "La alerta no existe."));
        }

        if (alerta.Estado != EstadosAlerta.Leida)
        {
            // RN-BD-26: el estado es lo único que cambia.
            alerta.Estado = EstadosAlerta.Leida;
            await _db.SaveChangesAsync(cancelacion);
            _log.LogInformation("Alertas: alerta_leida idUsuario={IdUsuario} idAlerta={IdAlerta}", idUsuario, idAlerta);
        }

        var respuesta = await ProyectarUsuario(_db.Alertas.AsNoTracking().Where(a => a.IdAlerta == idAlerta && a.IdUsuario == idUsuario))
            .SingleAsync(cancelacion);
        return Resultado<AlertaResponse>.Ok(respuesta);
    }

    /// <summary>
    /// Alertas originadas por las lecturas de un dispositivo del usuario (M12, RF-23). Un dispositivo ajeno o
    /// inexistente responde 404, igual que el resto de recursos propios.
    /// </summary>
    public async Task<Resultado<PaginaResponse<AlertaResponse>>> ListarPorDispositivoAsync(
        long idUsuario, long idDispositivo, int pagina, CancellationToken cancelacion)
    {
        if (!await _db.Dispositivos.AnyAsync(d => d.IdDispositivo == idDispositivo && d.IdUsuario == idUsuario, cancelacion))
        {
            return Resultado<PaginaResponse<AlertaResponse>>.Falla(new ErrorServicio(TipoError.NoEncontrado, "El dispositivo no existe."));
        }

        var consulta = _db.Alertas.AsNoTracking()
            .Where(a => a.IdUsuario == idUsuario && a.Lectura != null && a.Lectura.IdDispositivo == idDispositivo);
        var total = await consulta.CountAsync(cancelacion);

        var elementos = await ProyectarUsuario(Paginar(consulta, pagina)).ToListAsync(cancelacion);
        return Resultado<PaginaResponse<AlertaResponse>>.Ok(PaginaResponse<AlertaResponse>.Crear(elementos, pagina, total));
    }

    // ------------------------------------------------------------------ Administrador (M15, solo lectura)

    public async Task<Resultado<PaginaResponse<AlertaAdminResponse>>> ListarAdminAsync(FiltroAlertasAdminRequest filtro, CancellationToken cancelacion)
    {
        if (filtro.Desde is { } d && filtro.Hasta is { } h && d >= h)
        {
            return Resultado<PaginaResponse<AlertaAdminResponse>>.Falla(
                new ErrorServicio(TipoError.Validacion, "La fecha 'desde' debe ser anterior a 'hasta'.", "desde"));
        }

        var consulta = Filtrar(_db.Alertas.AsNoTracking(), filtro);

        if (filtro.IdUsuario is { } idUsuario)
        {
            consulta = consulta.Where(a => a.IdUsuario == idUsuario);
        }

        if (filtro.Origen == FiltrosAlerta.OrigenOpenWeather)
        {
            consulta = consulta.Where(a => a.IdConsulta != null);
        }
        else if (filtro.Origen == FiltrosAlerta.OrigenEsp32)
        {
            consulta = consulta.Where(a => a.IdLectura != null);
        }

        if (filtro.Desde is { } desde)
        {
            var desdeUtc = desde.UtcDateTime;
            consulta = consulta.Where(a => a.FechaGeneracion >= desdeUtc);
        }

        if (filtro.Hasta is { } hasta)
        {
            var hastaUtc = hasta.UtcDateTime;
            consulta = consulta.Where(a => a.FechaGeneracion < hastaUtc);
        }

        var total = await consulta.CountAsync(cancelacion);
        var elementos = await ProyectarAdmin(Paginar(consulta, filtro.Pagina)).ToListAsync(cancelacion);
        return Resultado<PaginaResponse<AlertaAdminResponse>>.Ok(PaginaResponse<AlertaAdminResponse>.Crear(elementos, filtro.Pagina, total));
    }

    public async Task<Resultado<AlertaAdminResponse>> ObtenerAdminAsync(long idAlerta, CancellationToken cancelacion)
    {
        var alerta = await ProyectarAdmin(_db.Alertas.AsNoTracking().Where(a => a.IdAlerta == idAlerta)).SingleOrDefaultAsync(cancelacion);
        return alerta is null
            ? Resultado<AlertaAdminResponse>.Falla(new ErrorServicio(TipoError.NoEncontrado, "La alerta no existe."))
            : Resultado<AlertaAdminResponse>.Ok(alerta);
    }

    // ------------------------------------------------------------------ Consultas

    private static IQueryable<Alerta> Filtrar(IQueryable<Alerta> alertas, FiltroAlertasRequest filtro)
    {
        if (filtro.Estado is { } estado)
        {
            alertas = alertas.Where(a => a.Estado == estado);
        }

        if (filtro.Tipo is { } tipo)
        {
            alertas = alertas.Where(a => a.Tipo == tipo);
        }

        return alertas;
    }

    /// <summary>Más recientes primero (índice ix_alerta_usuario_fecha). El id desempata.</summary>
    private static IQueryable<Alerta> Paginar(IQueryable<Alerta> alertas, int pagina) =>
        alertas
            .OrderByDescending(a => a.FechaGeneracion)
            .ThenByDescending(a => a.IdAlerta)
            .Skip((pagina - 1) * Paginacion.TamanoPagina)
            .Take(Paginacion.TamanoPagina);

    private static IQueryable<AlertaResponse> ProyectarUsuario(IQueryable<Alerta> alertas) =>
        alertas.Select(a => new AlertaResponse(
            a.IdAlerta,
            a.Tipo,
            a.Mensaje,
            a.Estado,
            a.ValorDetectado,
            a.ValorUmbral,
            a.FechaGeneracion,
            new UbicacionResponse(a.Ubicacion.IdUbicacion, a.Ubicacion.Nombre, a.Ubicacion.Region, a.Ubicacion.Pais, a.Ubicacion.Latitud, a.Ubicacion.Longitud),
            new OrigenAlertaResponse(
                a.IdLectura != null ? FiltrosAlerta.OrigenEsp32 : FiltrosAlerta.OrigenOpenWeather,
                a.IdConsulta,
                a.IdLectura,
                a.Lectura != null ? (long?)a.Lectura.IdDispositivo : null,
                a.Lectura != null ? a.Lectura.Dispositivo.Nombre : null)));

    private static IQueryable<AlertaAdminResponse> ProyectarAdmin(IQueryable<Alerta> alertas) =>
        alertas.Select(a => new AlertaAdminResponse(
            a.IdAlerta,
            a.Tipo,
            a.Mensaje,
            a.Estado,
            a.ValorDetectado,
            a.ValorUmbral,
            a.FechaGeneracion,
            new UbicacionResponse(a.Ubicacion.IdUbicacion, a.Ubicacion.Nombre, a.Ubicacion.Region, a.Ubicacion.Pais, a.Ubicacion.Latitud, a.Ubicacion.Longitud),
            new OrigenAlertaResponse(
                a.IdLectura != null ? FiltrosAlerta.OrigenEsp32 : FiltrosAlerta.OrigenOpenWeather,
                a.IdConsulta,
                a.IdLectura,
                a.Lectura != null ? (long?)a.Lectura.IdDispositivo : null,
                a.Lectura != null ? a.Lectura.Dispositivo.Nombre : null),
            new UsuarioAlertaResponse(a.Usuario.IdUsuario, a.Usuario.Nombre, a.Usuario.Correo)));
}
