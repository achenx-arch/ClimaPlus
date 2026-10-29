using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ClimaPlus.Api.DTOs.Comun;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.DTOs.Dispositivos;

/// <summary>Reglas de entrada de los dispositivos (diccionario de datos, tabla dispositivo).</summary>
public static class ReglasDispositivo
{
    public const int NombreMax = 100;   // dispositivo.nombre VARCHAR(100)

    /// <summary>Letras, dígitos, espacios y . _ - # ( ). Rechaza &lt; &gt; y otros símbolos.</summary>
    public const string PatronNombre = @"^\s*[\p{L}\p{M}\p{Nd}][\p{L}\p{M}\p{Nd} ._\-#()]*\s*$";

    public const string MensajeNombre = "El nombre solo puede contener letras, números, espacios y los signos . _ - # ( ).";
}

/// <summary>
/// Campos comunes de registro y edición (M11): nombre y ubicación. El dueño siempre es el usuario del token.
/// Cada clase concreta declara Disallow: System.Text.Json no lo hereda de la base.
/// </summary>
public abstract class DatosDispositivo
{
    [Required(ErrorMessage = "Escribe un nombre para el dispositivo.")]
    [StringLength(ReglasDispositivo.NombreMax, ErrorMessage = "El nombre admite como máximo 100 caracteres.")]
    [RegularExpression(ReglasDispositivo.PatronNombre, ErrorMessage = ReglasDispositivo.MensajeNombre)]
    public string? Nombre { get; init; }

    /// <summary>Id de una ubicación ya guardada (GET /api/clima/ubicaciones), igual que en favoritos (D-22).</summary>
    [Required(ErrorMessage = "Selecciona una ubicación.")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "La ubicación no es válida.")]
    public long? IdUbicacion { get; init; }
}

/// <summary>POST /api/dispositivos (RF-21).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CrearDispositivoRequest : DatosDispositivo
{
}

/// <summary>PUT /api/dispositivos/{id} (RF-22): editar nombre y ubicación.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ActualizarDispositivoRequest : DatosDispositivo
{
}

/// <summary>PATCH /api/dispositivos/{id}/estado (RF-22). FALSE revoca de inmediato la API Key (D-11).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CambiarEstadoDispositivoRequest
{
    [Required(ErrorMessage = "Indica si el dispositivo está activo.")]
    public bool? Activo { get; init; }
}

/// <summary>GET /api/dispositivos/{id}/lecturas y /alertas: paginación de 20 (08-mockups.md).</summary>
public sealed class PaginaDispositivoRequest
{
    [FromQuery(Name = "pagina")]
    [Range(1, Paginacion.PaginaMaxima, ErrorMessage = "La página debe ser un número mayor o igual que 1.")]
    public int Pagina { get; set; } = 1;
}
