using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Clima;
using ClimaPlus.Api.DTOs.Comun;
using ClimaPlus.Api.DTOs.Dispositivos;
using ClimaPlus.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Dispositivos;

/// <summary>
/// Dispositivos ESP32 del usuario autenticado (Fase 7, RF-20 a RF-23, M11 y M12). Todas las consultas filtran por el
/// id del token: un dispositivo ajeno se trata igual que uno inexistente (404), para no revelar que existe
/// (07-seguridad-inicial.md). Los dispositivos no se borran: se desactivan (D-11), lo que revoca su API Key.
/// </summary>
public sealed class DispositivoService
{
    /// <summary>
    /// Un dispositivo está "en línea" si su última lectura llegó hace 5 minutos o menos (DP-F7-1).
    /// El firmware envía una lectura por minuto, así que se toleran algunas pérdidas sin marcarlo fuera de línea.
    /// </summary>
    public static readonly TimeSpan VentanaEnLinea = TimeSpan.FromMinutes(5);

    public const string AvisoApiKey = "Guárdala ahora; no podrás verla de nuevo. Si la pierdes, registra el dispositivo otra vez.";

    private readonly ClimaPlusDbContext _db;
    private readonly TimeProvider _reloj;
    private readonly ILogger<DispositivoService> _log;

    public DispositivoService(ClimaPlusDbContext db, TimeProvider reloj, ILogger<DispositivoService> log)
    {
        _db = db;
        _reloj = reloj;
        _log = log;
    }

    public static bool EstaEnLinea(DateTime? ultimaConexion, DateTime ahoraUtc) =>
        ultimaConexion is { } ultima && ahoraUtc - ultima <= VentanaEnLinea;

    // ------------------------------------------------------------------ RF-20 / RF-23

    /// <summary>Dispositivos del usuario, del más reciente al más antiguo (M11).</summary>
    public async Task<Resultado<IReadOnlyList<DispositivoResponse>>> ListarAsync(long idUsuario, CancellationToken cancelacion)
    {
        var dispositivos = await Proyectar(DelUsuario(idUsuario)
                .OrderByDescending(d => d.FechaCreacion)
                .ThenByDescending(d => d.IdDispositivo))
            .ToListAsync(cancelacion);

        var ahora = Ahora();
        return Resultado<IReadOnlyList<DispositivoResponse>>.Ok(dispositivos.Select(d => ConEstadoConexion(d, ahora)).ToList());
    }

    public async Task<Resultado<DispositivoResponse>> ObtenerAsync(long idUsuario, long idDispositivo, CancellationToken cancelacion)
    {
        var dispositivo = await Proyectar(DelUsuario(idUsuario).Where(d => d.IdDispositivo == idDispositivo))
            .SingleOrDefaultAsync(cancelacion);

        return dispositivo is null
            ? Resultado<DispositivoResponse>.Falla(NoEncontrado())
            : Resultado<DispositivoResponse>.Ok(ConEstadoConexion(dispositivo, Ahora()));
    }

    /// <summary>Lecturas del dispositivo, las más recientes primero, de 20 en 20 (M12).</summary>
    public async Task<Resultado<PaginaResponse<LecturaResponse>>> ListarLecturasAsync(
        long idUsuario, long idDispositivo, int pagina, CancellationToken cancelacion)
    {
        if (!await DelUsuario(idUsuario).AnyAsync(d => d.IdDispositivo == idDispositivo, cancelacion))
        {
            return Resultado<PaginaResponse<LecturaResponse>>.Falla(NoEncontrado());
        }

        var lecturas = _db.LecturasSensor.AsNoTracking().Where(l => l.IdDispositivo == idDispositivo);
        var total = await lecturas.CountAsync(cancelacion);

        // Índice ix_lectura_sensor_dispositivo_fecha (id_dispositivo, fecha_lectura DESC). El id desempata.
        var elementos = await lecturas
            .OrderByDescending(l => l.FechaLectura)
            .ThenByDescending(l => l.IdLectura)
            .Skip((pagina - 1) * Paginacion.TamanoPagina)
            .Take(Paginacion.TamanoPagina)
            .Select(l => new LecturaResponse(l.IdLectura, l.FechaLectura, l.Temperatura, l.Humedad, l.Lluvia, l.Luz))
            .ToListAsync(cancelacion);

        return Resultado<PaginaResponse<LecturaResponse>>.Ok(PaginaResponse<LecturaResponse>.Crear(elementos, pagina, total));
    }

    // ------------------------------------------------------------------ RF-21

    /// <summary>Registra el dispositivo y devuelve su API Key una sola vez. Solo se guarda el hash (RN-BD-28).</summary>
    public async Task<Resultado<DispositivoRegistradoResponse>> RegistrarAsync(
        long idUsuario, CrearDispositivoRequest solicitud, CancellationToken cancelacion)
    {
        var nombre = Normalizacion.Nombre(solicitud.Nombre!);
        var idUbicacion = solicitud.IdUbicacion!.Value;

        var error = await ValidarAsync(idUsuario, null, nombre, idUbicacion, cancelacion);
        if (error is not null)
        {
            return Resultado<DispositivoRegistradoResponse>.Falla(error);
        }

        var apiKey = ApiKeyDispositivo.Generar();
        var dispositivo = new Dispositivo
        {
            IdUsuario = idUsuario,
            IdUbicacion = idUbicacion,
            Nombre = nombre,
            ApiKeyHash = ApiKeyDispositivo.Hash(apiKey),
            Activo = true,
            UltimaConexion = null,
            FechaCreacion = Ahora(),
        };
        _db.Dispositivos.Add(dispositivo);

        try
        {
            await _db.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException ex) when (AuthService.EsViolacionDeUnicidad(ex))
        {
            // Dos peticiones simultáneas con el mismo nombre (uq_dispositivo_usuario_nombre).
            _db.Entry(dispositivo).State = EntityState.Detached;
            return Resultado<DispositivoRegistradoResponse>.Falla(NombreDuplicado());
        }

        // Nunca se registra la clave ni su hash.
        _log.LogInformation("Dispositivos: dispositivo_registrado idUsuario={IdUsuario} idDispositivo={IdDispositivo} idUbicacion={IdUbicacion}",
            idUsuario, dispositivo.IdDispositivo, idUbicacion);

        var registrado = await ObtenerAsync(idUsuario, dispositivo.IdDispositivo, cancelacion);
        return Resultado<DispositivoRegistradoResponse>.Ok(new DispositivoRegistradoResponse(registrado.Valor!, apiKey, AvisoApiKey));
    }

    // ------------------------------------------------------------------ RF-22

    /// <summary>Cambia nombre y ubicación. Las lecturas siguientes se evaluarán con la ubicación nueva (RN-BD-33).</summary>
    public async Task<Resultado<DispositivoResponse>> ActualizarAsync(
        long idUsuario, long idDispositivo, ActualizarDispositivoRequest solicitud, CancellationToken cancelacion)
    {
        var dispositivo = await _db.Dispositivos
            .SingleOrDefaultAsync(d => d.IdDispositivo == idDispositivo && d.IdUsuario == idUsuario, cancelacion);
        if (dispositivo is null)
        {
            return Resultado<DispositivoResponse>.Falla(NoEncontrado());
        }

        var nombre = Normalizacion.Nombre(solicitud.Nombre!);
        var idUbicacion = solicitud.IdUbicacion!.Value;

        var error = await ValidarAsync(idUsuario, idDispositivo, nombre, idUbicacion, cancelacion);
        if (error is not null)
        {
            return Resultado<DispositivoResponse>.Falla(error);
        }

        dispositivo.Nombre = nombre;
        dispositivo.IdUbicacion = idUbicacion;

        try
        {
            await _db.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException ex) when (AuthService.EsViolacionDeUnicidad(ex))
        {
            _db.Entry(dispositivo).State = EntityState.Detached;
            return Resultado<DispositivoResponse>.Falla(NombreDuplicado());
        }

        _log.LogInformation("Dispositivos: dispositivo_actualizado idUsuario={IdUsuario} idDispositivo={IdDispositivo} idUbicacion={IdUbicacion}",
            idUsuario, idDispositivo, idUbicacion);
        return await ObtenerAsync(idUsuario, idDispositivo, cancelacion);
    }

    /// <summary>Activa o desactiva el dispositivo. Desactivarlo revoca de inmediato su API Key (D-11, RN-BD-29). Idempotente.</summary>
    public async Task<Resultado<DispositivoResponse>> CambiarEstadoAsync(
        long idUsuario, long idDispositivo, CambiarEstadoDispositivoRequest solicitud, CancellationToken cancelacion)
    {
        var dispositivo = await _db.Dispositivos
            .SingleOrDefaultAsync(d => d.IdDispositivo == idDispositivo && d.IdUsuario == idUsuario, cancelacion);
        if (dispositivo is null)
        {
            return Resultado<DispositivoResponse>.Falla(NoEncontrado());
        }

        var activo = solicitud.Activo!.Value;
        if (dispositivo.Activo != activo)
        {
            dispositivo.Activo = activo;
            await _db.SaveChangesAsync(cancelacion);
            _log.LogInformation("Dispositivos: estado_cambiado idUsuario={IdUsuario} idDispositivo={IdDispositivo} activo={Activo}",
                idUsuario, idDispositivo, activo);
        }

        return await ObtenerAsync(idUsuario, idDispositivo, cancelacion);
    }

    // ------------------------------------------------------------------ Auxiliares

    /// <summary>Ubicación existente (404) y nombre único por usuario (RN-BD-30, 409). La BD lo garantiza con UNIQUE.</summary>
    private async Task<ErrorServicio?> ValidarAsync(
        long idUsuario, long? idDispositivoActual, string nombre, long idUbicacion, CancellationToken cancelacion)
    {
        if (!await _db.Ubicaciones.AnyAsync(u => u.IdUbicacion == idUbicacion, cancelacion))
        {
            return new ErrorServicio(TipoError.NoEncontrado, "La ubicación no existe. Búscala de nuevo para seleccionarla.", "idUbicacion");
        }

        var duplicado = await _db.Dispositivos.AnyAsync(
            d => d.IdUsuario == idUsuario && d.Nombre == nombre && d.IdDispositivo != idDispositivoActual, cancelacion);
        return duplicado ? NombreDuplicado() : null;
    }

    /// <summary>Punto único de aislamiento: solo dispositivos cuyo dueño es el usuario del token.</summary>
    private IQueryable<Dispositivo> DelUsuario(long idUsuario) =>
        _db.Dispositivos.AsNoTracking().Where(d => d.IdUsuario == idUsuario);

    /// <summary>Proyección traducible a SQL. EnLinea se completa después con el reloj de la API.</summary>
    private static IQueryable<DispositivoResponse> Proyectar(IQueryable<Dispositivo> dispositivos) =>
        dispositivos.Select(d => new DispositivoResponse(
            d.IdDispositivo,
            d.Nombre,
            d.Activo,
            false,
            d.UltimaConexion,
            d.FechaCreacion,
            new UbicacionResponse(d.Ubicacion.IdUbicacion, d.Ubicacion.Nombre, d.Ubicacion.Region, d.Ubicacion.Pais, d.Ubicacion.Latitud, d.Ubicacion.Longitud),
            d.Lecturas
                .OrderByDescending(l => l.FechaLectura)
                .ThenByDescending(l => l.IdLectura)
                .Select(l => new LecturaResponse(l.IdLectura, l.FechaLectura, l.Temperatura, l.Humedad, l.Lluvia, l.Luz))
                .FirstOrDefault()));

    private static DispositivoResponse ConEstadoConexion(DispositivoResponse dispositivo, DateTime ahora) =>
        dispositivo with { EnLinea = EstaEnLinea(dispositivo.UltimaConexion, ahora) };

    /// <summary>Microsegundos, igual que timestamptz.</summary>
    private DateTime Ahora()
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        return new DateTime(ahora.Ticks - (ahora.Ticks % 10), DateTimeKind.Utc);
    }

    private static ErrorServicio NoEncontrado() =>
        new(TipoError.NoEncontrado, "El dispositivo no existe.");

    private static ErrorServicio NombreDuplicado() =>
        new(TipoError.Conflicto, "Ya tienes un dispositivo con ese nombre.", "nombre");
}
