using ClimaPlus.Api.DTOs.Usuarios;

namespace ClimaPlus.Api.DTOs.Auth;

/// <summary>Respuesta de un inicio de sesión exitoso. ExpiraEn está en UTC.</summary>
public sealed record LoginResponse(string Token, string TipoToken, DateTime ExpiraEn, UsuarioResponse Usuario);
