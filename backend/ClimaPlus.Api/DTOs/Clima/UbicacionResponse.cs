using ClimaPlus.Api.Domain.Entities;

namespace ClimaPlus.Api.DTOs.Clima;

/// <summary>Ubicación persistida en la tabla ubicacion. IdUbicacion es el que usan clima, pronóstico y favoritos.</summary>
public sealed record UbicacionResponse(long IdUbicacion, string Nombre, string? Region, string Pais, decimal Latitud, decimal Longitud)
{
    public static UbicacionResponse Desde(Ubicacion u) =>
        new(u.IdUbicacion, u.Nombre, u.Region, u.Pais, u.Latitud, u.Longitud);
}
