using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClimaPlus.Api.DTOs.Lecturas;

/// <summary>
/// POST /api/lecturas (RF-24), enviado por el ESP32 con la cabecera X-Device-Key.
/// Rangos de RN-BD-31 (los mismos CHECK de lectura_sensor). Cada medición es opcional, pero debe venir al menos una.
/// La fecha la asigna el servidor (RN-BD-32): el cuerpo no acepta campos adicionales.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LecturaRequest
{
    /// <summary>°C, de -40 a 85 (rango del BME280). Se guarda con 2 decimales.</summary>
    [Range(typeof(decimal), "-40", "85", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "La temperatura debe estar entre -40 y 85 °C.")]
    public decimal? Temperatura { get; init; }

    /// <summary>%, de 0 a 100. Se guarda con 2 decimales.</summary>
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "La humedad debe estar entre 0 y 100 %.")]
    public decimal? Humedad { get; init; }

    /// <summary>true si el sensor detecta lluvia (salida digital, D-08).</summary>
    public bool? Lluvia { get; init; }

    /// <summary>Luz normalizada por el firmware, entero de 0 a 100 % (D-08).</summary>
    [Range(0, 100, ErrorMessage = "La luz debe ser un entero entre 0 y 100 %.")]
    public short? Luz { get; init; }

    public bool TieneMediciones() => Temperatura is not null || Humedad is not null || Lluvia is not null || Luz is not null;
}
