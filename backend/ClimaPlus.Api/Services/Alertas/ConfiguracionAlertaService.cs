using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Alertas;
using ClimaPlus.Api.DTOs.Clima;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Alertas;

/// <summary>
/// Reglas de alerta del usuario (Fase 2H, M10). Aplica RN-BD-16 a RN-BD-19 y RN-BD-21 antes
/// de llegar a PostgreSQL, que vuelve a garantizarlas con CHECK y UNIQUE NULLS NOT DISTINCT.
/// Una regla ajena se trata igual que una inexistente (404).
/// La evaluación de reglas NO está aquí: la hacen EvaluadorAlertas y MotorAlertas (Fase 2H).
/// </summary>
public sealed class ConfiguracionAlertaService
{
    public const decimal UmbralMin = -50m;
    public const decimal UmbralMax = 60m;

    private readonly ClimaPlusDbContext _db;
    private readonly ILogger<ConfiguracionAlertaService> _log;

    public ConfiguracionAlertaService(ClimaPlusDbContext db, ILogger<ConfiguracionAlertaService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<Resultado<IReadOnlyList<ConfiguracionAlertaResponse>>> ListarAsync(long idUsuario, CancellationToken cancelacion)
    {
        var reglas = await Proyectar(DelUsuario(idUsuario)
                .OrderByDescending(c => c.FechaCreacion)
                .ThenByDescending(c => c.IdConfiguracion))
            .ToListAsync(cancelacion);

        return Resultado<IReadOnlyList<ConfiguracionAlertaResponse>>.Ok(reglas);
    }

    public async Task<Resultado<ConfiguracionAlertaResponse>> ObtenerAsync(long idUsuario, long idConfiguracion, CancellationToken cancelacion)
    {
        var regla = await Proyectar(DelUsuario(idUsuario).Where(c => c.IdConfiguracion == idConfiguracion))
            .SingleOrDefaultAsync(cancelacion);

        return regla is null
            ? Resultado<ConfiguracionAlertaResponse>.Falla(NoEncontrada())
            : Resultado<ConfiguracionAlertaResponse>.Ok(regla);
    }

    public async Task<Resultado<ConfiguracionAlertaResponse>> CrearAsync(long idUsuario, CrearConfiguracionAlertaRequest solicitud, CancellationToken cancelacion)
    {
        var error = ValidarCampos(solicitud)
            ?? await ValidarContraOtrasReglasAsync(idUsuario, null, solicitud.Tipo!, solicitud.IdUbicacion, solicitud.ValorUmbral, cancelacion);
        if (error is not null)
        {
            return Resultado<ConfiguracionAlertaResponse>.Falla(error);
        }

        var regla = new ConfiguracionAlerta
        {
            IdUsuario = idUsuario,
            IdUbicacion = solicitud.IdUbicacion,
            Tipo = solicitud.Tipo!,
            ValorUmbral = solicitud.ValorUmbral,
            Activa = solicitud.Activa ?? true,
            FechaCreacion = DateTime.UtcNow,
        };
        _db.ConfiguracionesAlerta.Add(regla);

        if (!await GuardarAsync(regla, cancelacion))
        {
            return Resultado<ConfiguracionAlertaResponse>.Falla(Duplicada());
        }

        _log.LogInformation("Alertas: regla_creada idUsuario={IdUsuario} idConfiguracion={IdConfiguracion} tipo={Tipo}",
            idUsuario, regla.IdConfiguracion, regla.Tipo);
        return await ObtenerAsync(idUsuario, regla.IdConfiguracion, cancelacion);
    }

    public async Task<Resultado<ConfiguracionAlertaResponse>> ActualizarAsync(
        long idUsuario, long idConfiguracion, ActualizarConfiguracionAlertaRequest solicitud, CancellationToken cancelacion)
    {
        var regla = await BuscarPropiaAsync(idUsuario, idConfiguracion, cancelacion);
        if (regla is null)
        {
            return Resultado<ConfiguracionAlertaResponse>.Falla(NoEncontrada());
        }

        var error = ValidarCampos(solicitud)
            ?? await ValidarContraOtrasReglasAsync(idUsuario, idConfiguracion, solicitud.Tipo!, solicitud.IdUbicacion, solicitud.ValorUmbral, cancelacion);
        if (error is not null)
        {
            return Resultado<ConfiguracionAlertaResponse>.Falla(error);
        }

        regla.Tipo = solicitud.Tipo!;
        regla.IdUbicacion = solicitud.IdUbicacion;
        regla.ValorUmbral = solicitud.ValorUmbral;
        regla.Activa = solicitud.Activa!.Value;

        if (!await GuardarAsync(regla, cancelacion))
        {
            return Resultado<ConfiguracionAlertaResponse>.Falla(Duplicada());
        }

        _log.LogInformation("Alertas: regla_actualizada idUsuario={IdUsuario} idConfiguracion={IdConfiguracion}", idUsuario, idConfiguracion);
        return await ObtenerAsync(idUsuario, idConfiguracion, cancelacion);
    }

    /// <summary>Activa o pausa la regla sin modificarla. Una regla pausada no genera alertas (RN-BD-22).</summary>
    public async Task<Resultado<ConfiguracionAlertaResponse>> CambiarEstadoAsync(
        long idUsuario, long idConfiguracion, bool activa, CancellationToken cancelacion)
    {
        var regla = await BuscarPropiaAsync(idUsuario, idConfiguracion, cancelacion);
        if (regla is null)
        {
            return Resultado<ConfiguracionAlertaResponse>.Falla(NoEncontrada());
        }

        if (regla.Activa != activa)
        {
            regla.Activa = activa;
            await _db.SaveChangesAsync(cancelacion);
            _log.LogInformation("Alertas: regla_{Accion} idUsuario={IdUsuario} idConfiguracion={IdConfiguracion}",
                activa ? "activada" : "pausada", idUsuario, idConfiguracion);
        }

        return await ObtenerAsync(idUsuario, idConfiguracion, cancelacion);
    }

    /// <summary>
    /// Elimina la regla. Ninguna tabla referencia configuracion_alerta: las alertas ya generadas
    /// guardan su propio umbral (RN-BD-24) y se conservan.
    /// </summary>
    public async Task<Resultado> EliminarAsync(long idUsuario, long idConfiguracion, CancellationToken cancelacion)
    {
        var regla = await BuscarPropiaAsync(idUsuario, idConfiguracion, cancelacion);
        if (regla is null)
        {
            return Resultado.Falla(NoEncontrada());
        }

        _db.ConfiguracionesAlerta.Remove(regla);
        await _db.SaveChangesAsync(cancelacion);

        _log.LogInformation("Alertas: regla_eliminada idUsuario={IdUsuario} idConfiguracion={IdConfiguracion}", idUsuario, idConfiguracion);
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ Validación

    /// <summary>RN-BD-17 y RN-BD-18 (rango ya validado por el DTO) más la precisión de NUMERIC(5,2).</summary>
    private static ErrorServicio? ValidarCampos(DatosConfiguracionAlerta solicitud)
    {
        if (solicitud.Tipo == TiposAlerta.Lluvia)
        {
            return solicitud.ValorUmbral is null
                ? null
                : new ErrorServicio(TipoError.Validacion, "La alerta de lluvia no usa umbral; envía valorUmbral vacío.", "valorUmbral");
        }

        if (solicitud.ValorUmbral is not { } umbral)
        {
            return new ErrorServicio(TipoError.Validacion, "El umbral es obligatorio para las alertas de temperatura.", "valorUmbral");
        }

        if (umbral < UmbralMin || umbral > UmbralMax)
        {
            return new ErrorServicio(TipoError.Validacion, "El umbral debe estar entre -50 y 60 °C.", "valorUmbral");
        }

        if (decimal.Round(umbral, 2) != umbral)
        {
            return new ErrorServicio(TipoError.Validacion, "El umbral admite como máximo 2 decimales.", "valorUmbral");
        }

        return null;
    }

    /// <summary>
    /// Ubicación existente, RN-BD-19 (sin duplicados, incluida una sola regla global por tipo)
    /// y RN-BD-21 (para la misma ubicación, o ambas globales, alta &gt; baja).
    /// <paramref name="idExcluido"/> es la regla que se está editando.
    /// </summary>
    private async Task<ErrorServicio?> ValidarContraOtrasReglasAsync(
        long idUsuario, long? idExcluido, string tipo, long? idUbicacion, decimal? umbral, CancellationToken cancelacion)
    {
        if (idUbicacion is { } id && !await _db.Ubicaciones.AnyAsync(u => u.IdUbicacion == id, cancelacion))
        {
            return new ErrorServicio(TipoError.NoEncontrado, "La ubicación no existe. Búscala de nuevo para usarla.", "idUbicacion");
        }

        var otras = _db.ConfiguracionesAlerta
            .AsNoTracking()
            .Where(c => c.IdUsuario == idUsuario
                     && c.IdUbicacion == idUbicacion
                     && (idExcluido == null || c.IdConfiguracion != idExcluido));

        if (await otras.AnyAsync(c => c.Tipo == tipo, cancelacion))
        {
            return Duplicada();
        }

        var opuesto = tipo switch
        {
            TiposAlerta.TemperaturaAlta => TiposAlerta.TemperaturaBaja,
            TiposAlerta.TemperaturaBaja => TiposAlerta.TemperaturaAlta,
            _ => null,
        };
        if (opuesto is null)
        {
            return null;
        }

        var umbralOpuesto = await otras
            .Where(c => c.Tipo == opuesto)
            .Select(c => c.ValorUmbral)
            .FirstOrDefaultAsync(cancelacion);

        if (umbralOpuesto is { } otro)
        {
            var valido = tipo == TiposAlerta.TemperaturaAlta ? umbral > otro : umbral < otro;
            if (!valido)
            {
                var mensaje = tipo == TiposAlerta.TemperaturaAlta
                    ? $"El umbral de temperatura alta debe ser mayor que el de temperatura baja ({otro:0.##} °C) para esta ubicación."
                    : $"El umbral de temperatura baja debe ser menor que el de temperatura alta ({otro:0.##} °C) para esta ubicación.";
                return new ErrorServicio(TipoError.Conflicto, mensaje, "valorUmbral");
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ Acceso a datos

    /// <summary>Punto único de aislamiento: solo reglas cuyo dueño es el usuario del token.</summary>
    private IQueryable<ConfiguracionAlerta> DelUsuario(long idUsuario) =>
        _db.ConfiguracionesAlerta.AsNoTracking().Where(c => c.IdUsuario == idUsuario);

    private Task<ConfiguracionAlerta?> BuscarPropiaAsync(long idUsuario, long idConfiguracion, CancellationToken cancelacion) =>
        _db.ConfiguracionesAlerta.SingleOrDefaultAsync(c => c.IdConfiguracion == idConfiguracion && c.IdUsuario == idUsuario, cancelacion);

    /// <summary>Guarda; devuelve false si uq_configuracion_alerta_usuario_ubicacion_tipo rechaza la regla (peticiones simultáneas).</summary>
    private async Task<bool> GuardarAsync(ConfiguracionAlerta regla, CancellationToken cancelacion)
    {
        try
        {
            await _db.SaveChangesAsync(cancelacion);
            return true;
        }
        catch (DbUpdateException ex) when (AuthService.EsViolacionDeUnicidad(ex))
        {
            var entrada = _db.Entry(regla);
            if (entrada.State == EntityState.Added)
            {
                entrada.State = EntityState.Detached;
            }
            else
            {
                await entrada.ReloadAsync(cancelacion);
            }

            return false;
        }
    }

    private static IQueryable<ConfiguracionAlertaResponse> Proyectar(IQueryable<ConfiguracionAlerta> reglas) =>
        reglas.Select(c => new ConfiguracionAlertaResponse(
            c.IdConfiguracion,
            c.Tipo,
            c.Ubicacion == null
                ? null
                : new UbicacionResponse(
                    c.Ubicacion.IdUbicacion,
                    c.Ubicacion.Nombre,
                    c.Ubicacion.Region,
                    c.Ubicacion.Pais,
                    c.Ubicacion.Latitud,
                    c.Ubicacion.Longitud),
            c.ValorUmbral,
            c.Activa,
            c.FechaCreacion));

    private static ErrorServicio NoEncontrada() =>
        new(TipoError.NoEncontrado, "La regla de alerta no existe.");

    private static ErrorServicio Duplicada() =>
        new(TipoError.Conflicto, "Ya tienes una regla de ese tipo para esta ubicación.", "tipo");
}
