using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ClimaPlus.Api.Security;

namespace ClimaPlus.Api.DTOs.Auth;

/// <summary>
/// Respuesta 202 de los pasos que envían un código (registro y, en fases siguientes, login, Google y recuperación).
/// Igual para cuentas reales y señuelos. Destino = correo enmascarado ("a***@gmail.com"). ExpiraEn en UTC.
/// </summary>
public sealed record RespuestaDesafio(string Desafio, string Proposito, string Destino, DateTime ExpiraEn, int ReenviarEn);

/// <summary>Respuesta 202 de POST /api/auth/reenviar-codigo (sin destino: el cliente ya lo tiene).</summary>
public sealed record RespuestaReenvio(string Desafio, string Proposito, DateTime ExpiraEn, int ReenviarEn);

/// <summary>Cuerpo de los endpoints que validan un código de 6 dígitos.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VerificarCodigoRequest
{
    [Required(ErrorMessage = "Falta el desafío. Vuelve a empezar.")]
    [StringLength(TokenDesafio.LongitudMaxima, ErrorMessage = "El desafío no es válido.")]
    public string Desafio { get; init; } = string.Empty;

    [Required(ErrorMessage = "Escribe el código de 6 dígitos.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "El código tiene 6 dígitos.")]
    public string Codigo { get; init; } = string.Empty;
}

/// <summary>Cuerpo de POST /api/auth/reenviar-codigo.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReenviarCodigoRequest
{
    [Required(ErrorMessage = "Falta el desafío. Vuelve a empezar.")]
    [StringLength(TokenDesafio.LongitudMaxima, ErrorMessage = "El desafío no es válido.")]
    public string Desafio { get; init; } = string.Empty;
}
