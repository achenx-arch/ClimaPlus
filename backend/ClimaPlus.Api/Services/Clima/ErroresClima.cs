using ClimaPlus.Api.Integraciones.OpenWeather;

namespace ClimaPlus.Api.Services.Clima;

/// <summary>
/// Traducción de errores del proveedor a errores de servicio (D-24).
/// Los mensajes son genéricos: nunca exponen detalles de OpenWeather ni la configuración.
/// </summary>
public static class ErroresClima
{
    public static ErrorServicio Desde(ErrorProveedor error) => error switch
    {
        ErrorProveedor.NoEncontrado => new(TipoError.NoEncontrado, "No se encontró información meteorológica para la ubicación solicitada."),
        ErrorProveedor.TiempoAgotado => TiempoAgotado(),
        ErrorProveedor.RespuestaInvalida => RespuestaInvalida(),
        _ => new(TipoError.ServicioNoDisponible, "El servicio meteorológico no está disponible en este momento. Inténtalo más tarde."),
    };

    public static ErrorServicio TiempoAgotado() =>
        new(TipoError.TiempoAgotado, "El servicio meteorológico tardó demasiado en responder. Inténtalo más tarde.");

    public static ErrorServicio RespuestaInvalida() =>
        new(TipoError.RespuestaProveedorInvalida, "El servicio meteorológico devolvió datos que no se pueden procesar.");
}
