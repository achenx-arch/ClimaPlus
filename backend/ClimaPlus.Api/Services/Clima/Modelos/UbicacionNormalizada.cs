namespace ClimaPlus.Api.Services.Clima.Modelos;

/// <summary>Resultado de Geocoding ya validado contra las restricciones de la tabla ubicacion.</summary>
public sealed record UbicacionNormalizada(string Nombre, string? Region, string Pais, decimal Latitud, decimal Longitud);
