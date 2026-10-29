using ClimaPlus.Api.Services.Clima.Modelos;

namespace ClimaPlus.Api.Services.Clima;

/// <summary>
/// Persistencia de consultas meteorológicas en consulta_clima (guía, sección 4: guardar antes de mostrar).
/// La base de datos actúa como caché (D-23): 10 min para ACTUAL y 30 min para PRONOSTICO.
/// </summary>
public interface IRegistroConsultaClima
{
    /// <summary>
    /// Datos de una consulta ACTUAL de la ubicación obtenidos de OpenWeather hace menos de
    /// <paramref name="vigencia"/>, de cualquier usuario o visitante; null si no hay.
    /// </summary>
    Task<ClimaActualNormalizado?> BuscarActualVigenteAsync(long idUbicacion, TimeSpan vigencia, CancellationToken cancelacion);

    /// <summary>Igual que <see cref="BuscarActualVigenteAsync"/>, para PRONOSTICO.</summary>
    Task<DetallePronostico?> BuscarPronosticoVigenteAsync(long idUbicacion, TimeSpan vigencia, CancellationToken cancelacion);

    /// <summary>Guarda una consulta ACTUAL (idUsuario null = visitante, E-02) y devuelve lo leído de la base de datos.</summary>
    Task<ClimaActualGuardado> RegistrarActualAsync(long idUbicacion, long? idUsuario, ClimaActualNormalizado clima, bool desdeCache, CancellationToken cancelacion);

    /// <summary>Guarda una consulta PRONOSTICO (mediciones NULL, datos en detalle, E-03) y devuelve lo leído de la base de datos.</summary>
    Task<PronosticoGuardado> RegistrarPronosticoAsync(long idUbicacion, long? idUsuario, DetallePronostico pronostico, bool desdeCache, CancellationToken cancelacion);
}

/// <summary>Consulta ACTUAL leída de consulta_clima.</summary>
public sealed record ClimaActualGuardado(long IdConsulta, DateTime FechaConsultaUtc, bool DesdeCache, ClimaActualNormalizado Clima);

/// <summary>Consulta PRONOSTICO leída de consulta_clima.</summary>
public sealed record PronosticoGuardado(long IdConsulta, DateTime FechaConsultaUtc, bool DesdeCache, DetallePronostico Pronostico);
