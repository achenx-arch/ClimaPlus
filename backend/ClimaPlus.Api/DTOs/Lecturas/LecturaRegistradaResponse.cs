using ClimaPlus.Api.DTOs.Alertas;

namespace ClimaPlus.Api.DTOs.Lecturas;

/// <summary>
/// Respuesta de POST /api/lecturas. <c>AlertasGeneradas</c> permite al ESP32 activar el LED rojo y el buzzer
/// (alertas físicas) sin hacer otra petición.
/// </summary>
public sealed record LecturaRegistradaResponse(
    long IdLectura,
    long IdDispositivo,
    DateTime FechaLectura,
    IReadOnlyList<AlertaGeneradaResponse> AlertasGeneradas);
