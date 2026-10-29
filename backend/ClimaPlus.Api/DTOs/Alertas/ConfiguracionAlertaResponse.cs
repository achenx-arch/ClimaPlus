using ClimaPlus.Api.DTOs.Clima;

namespace ClimaPlus.Api.DTOs.Alertas;

/// <summary>Regla de alerta del usuario autenticado (M10). Ubicacion null = "Todas mis ubicaciones".</summary>
public sealed record ConfiguracionAlertaResponse(
    long IdConfiguracion,
    string Tipo,
    UbicacionResponse? Ubicacion,
    decimal? ValorUmbral,
    bool Activa,
    DateTime FechaCreacion);
