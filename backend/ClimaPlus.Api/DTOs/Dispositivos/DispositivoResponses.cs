using ClimaPlus.Api.DTOs.Clima;

namespace ClimaPlus.Api.DTOs.Dispositivos;

/// <summary>
/// Dispositivo del usuario (M11, M12). Nunca incluye la API Key ni su hash (RN-BD-28).
/// <c>EnLinea</c> se calcula con <c>ultima_conexion</c> (D-11, D-12): true si llegó una lectura en los últimos 5 minutos.
/// </summary>
public sealed record DispositivoResponse(
    long IdDispositivo,
    string Nombre,
    bool Activo,
    bool EnLinea,
    DateTime? UltimaConexion,
    DateTime FechaCreacion,
    UbicacionResponse Ubicacion,
    LecturaResponse? UltimaLectura);

/// <summary>
/// Respuesta de POST /api/dispositivos. <c>ApiKey</c> se muestra UNA sola vez (RN-BD-28): el servidor solo guarda su
/// hash SHA-256 y no puede volver a mostrarla.
/// </summary>
public sealed record DispositivoRegistradoResponse(DispositivoResponse Dispositivo, string ApiKey, string Aviso);

/// <summary>Lectura de sensores (M12). Las mediciones que el ESP32 no envió son null (RN-BD-31).</summary>
public sealed record LecturaResponse(
    long IdLectura,
    DateTime FechaLectura,
    decimal? Temperatura,
    decimal? Humedad,
    bool? Lluvia,
    short? Luz);
