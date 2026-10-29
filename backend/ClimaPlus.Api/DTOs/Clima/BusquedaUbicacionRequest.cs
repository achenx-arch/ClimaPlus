using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.DTOs.Clima;

/// <summary>Parámetros de GET /api/clima/ubicaciones (C5: búsqueda solo por nombre).</summary>
public sealed class BusquedaUbicacionRequest
{
    public const int LimiteMaximo = 5;

    /// <summary>Letras (con tildes y ñ), dígitos, espacios, punto, coma, apóstrofo y guion. Rechaza &lt; &gt; ; y otros símbolos.</summary>
    public const string PatronConsulta = @"^\s*[\p{L}\p{M}\d][\p{L}\p{M}\d .,'\-]*\s*$";

    [FromQuery(Name = "q")]
    [Required(ErrorMessage = "Escribe el nombre de una ciudad.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "La búsqueda debe tener entre 2 y 100 caracteres.")]
    [RegularExpression(PatronConsulta, ErrorMessage = "La búsqueda solo puede contener letras, números, espacios, puntos, comas, apóstrofos y guiones.")]
    public string? Q { get; set; }

    [FromQuery(Name = "limite")]
    [Range(1, LimiteMaximo, ErrorMessage = "El límite debe estar entre 1 y 5.")]
    public int Limite { get; set; } = LimiteMaximo;
}
