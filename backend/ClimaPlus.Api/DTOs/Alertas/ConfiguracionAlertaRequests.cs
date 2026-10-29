using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClimaPlus.Api.DTOs.Alertas;

/// <summary>
/// Campos comunes de una regla de alerta (M10). Las reglas que combinan campos
/// (umbral según el tipo, 2 decimales, RN-BD-19 y RN-BD-21) se validan en ConfiguracionAlertaService.
/// El usuario se toma del token; nunca del cuerpo.
/// </summary>
public abstract class DatosConfiguracionAlerta
{
    public const string PatronTipo = "^(LLUVIA|TEMPERATURA_ALTA|TEMPERATURA_BAJA)$";

    /// <summary>LLUVIA, TEMPERATURA_ALTA o TEMPERATURA_BAJA (RN-BD-16).</summary>
    [Required(ErrorMessage = "Selecciona el tipo de alerta.")]
    [RegularExpression(PatronTipo, ErrorMessage = "El tipo debe ser LLUVIA, TEMPERATURA_ALTA o TEMPERATURA_BAJA.")]
    public string? Tipo { get; init; }

    /// <summary>null = la regla aplica a todas las ubicaciones del usuario (regla global).</summary>
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "La ubicación no es válida.")]
    public long? IdUbicacion { get; init; }

    /// <summary>°C. Obligatorio para temperatura; debe omitirse o ser null para LLUVIA (RN-BD-17).</summary>
    [Range(-50.0, 60.0, ErrorMessage = "El umbral debe estar entre -50 y 60 °C.")]
    public decimal? ValorUmbral { get; init; }
}

/// <summary>POST /api/configuraciones-alerta.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CrearConfiguracionAlertaRequest : DatosConfiguracionAlerta
{
    /// <summary>Opcional; por defecto true.</summary>
    public bool? Activa { get; init; }
}

/// <summary>PUT /api/configuraciones-alerta/{id}: reemplaza todos los campos editables.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ActualizarConfiguracionAlertaRequest : DatosConfiguracionAlerta
{
    [Required(ErrorMessage = "Indica si la regla está activa.")]
    public bool? Activa { get; init; }
}

/// <summary>PATCH /api/configuraciones-alerta/{id}/estado: switch activar/pausar de M10.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CambiarEstadoConfiguracionRequest
{
    [Required(ErrorMessage = "Indica si la regla está activa.")]
    public bool? Activa { get; init; }
}
