namespace ClimaPlus.Api.DTOs.Alertas;

/// <summary>
/// Alerta creada por la consulta de clima actual de esta misma petición (campo alertasGeneradas de
/// GET /api/clima/actual). Sirve para el toast "Nueva alerta: …" de M04; el detalle completo está en
/// GET /api/alertas.
/// </summary>
public sealed record AlertaGeneradaResponse(long IdAlerta, string Tipo, string Mensaje, DateTime FechaGeneracion);
