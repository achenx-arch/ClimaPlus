using System.Globalization;
using System.Security.Claims;
using System.Text;
using ClimaPlus.Api.Domain.Constantes;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Contenido de un token de desafío válido. <paramref name="ParaSesion"/> es true si lo emitió el primer paso de un
/// inicio de sesión (contraseña ya comprobada): solo esos desafíos pueden terminar en un JWT de sesión.
/// </summary>
/// <paramref name="CorreoExterno"/> solo existe en VINCULAR_GOOGLE: correo que informó Google, firmado por la API.
public sealed record DatosDesafio(Guid IdDesafio, string Proposito, bool ParaSesion = false, string? CorreoExterno = null);

/// <summary>
/// Token del desafío: un JWT corto (5 a 10 minutos) con audiencia "ClimaPlus.Desafio". Solo lleva el id del desafío
/// y su propósito, nunca el id ni el correo del usuario: así un señuelo no se distingue de uno real. El validador de
/// sesión exige la audiencia "ClimaPlus.Frontend", por eso este token no abre ningún endpoint privado.
/// También emite el permiso de restablecer la contraseña (audiencia "ClimaPlus.Restablecer", 10 minutos): solo
/// sirve en POST /api/auth/password/restablecer, nunca como sesión ni como desafío.
/// </summary>
public sealed class TokenDesafio
{
    public const string Audiencia = "ClimaPlus.Desafio";
    public const string AudienciaPermiso = "ClimaPlus.Restablecer";
    public const int LongitudMaxima = 2048;

    private const string ClaimDesafio = "did";
    private const string ClaimProposito = "prp";
    private const string ClaimSesion = "ses";
    private const string ClaimCorreoExterno = "gml";

    private readonly IOptions<JwtOptions> _jwt;
    private readonly TimeProvider _reloj;
    private readonly JsonWebTokenHandler _manejador = new();

    public TokenDesafio(IOptions<JwtOptions> jwt, TimeProvider reloj)
    {
        _jwt = jwt;
        _reloj = reloj;
    }

    private SymmetricSecurityKey Clave => new(Encoding.UTF8.GetBytes(_jwt.Value.Clave));

    public string Crear(Guid idDesafio, string proposito, DateTime expiraEnUtc, bool paraSesion = false, string? correoExterno = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimDesafio, idDesafio.ToString("N", CultureInfo.InvariantCulture)),
            new(ClaimProposito, proposito),
        };
        if (paraSesion)
        {
            claims.Add(new Claim(ClaimSesion, "1"));
        }

        if (correoExterno is not null)
        {
            claims.Add(new Claim(ClaimCorreoExterno, correoExterno));
        }

        return Firmar(Audiencia, claims, expiraEnUtc);
    }

    /// <summary>Permiso de restablecer la contraseña para un desafío RECUPERAR_PASSWORD ya VERIFICADO.</summary>
    public string CrearPermiso(Guid idDesafio, DateTime expiraEnUtc) =>
        Firmar(AudienciaPermiso, new List<Claim>
        {
            new(ClaimDesafio, idDesafio.ToString("N", CultureInfo.InvariantCulture)),
            new(ClaimProposito, PropositosDesafio.RecuperarPassword),
        }, expiraEnUtc);

    /// <summary>Id del desafío de un permiso de restablecer válido, o null si es inválido, está alterado o venció.</summary>
    public async Task<Guid?> LeerPermisoAsync(string? token)
    {
        var datos = await ValidarAsync(token, AudienciaPermiso);
        return datos?.Proposito == PropositosDesafio.RecuperarPassword ? datos.IdDesafio : null;
    }

    private string Firmar(string audiencia, List<Claim> claims, DateTime expiraEnUtc)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        return _manejador.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwt.Value.Emisor,
            Audience = audiencia,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expiraEnUtc,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(Clave, SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>Datos del desafío, o null si el token es inválido, está alterado o venció.</summary>
    public Task<DatosDesafio?> LeerAsync(string? token) => ValidarAsync(token, Audiencia);

    private async Task<DatosDesafio?> ValidarAsync(string? token, string audiencia)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > LongitudMaxima)
        {
            return null;
        }

        var resultado = await _manejador.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Value.Emisor,
            ValidateAudience = true,
            ValidAudience = audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = Clave,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateLifetime = true,
            // Misma hora que la base de datos (TimeProvider): el token y el código vencen a la vez.
            LifetimeValidator = (_, expira, _, _) => expira is { } fin && fin > _reloj.GetUtcNow().UtcDateTime,
        });

        if (!resultado.IsValid
            || !resultado.Claims.TryGetValue(ClaimDesafio, out var id)
            || !Guid.TryParseExact(id as string, "N", out var idDesafio)
            || !resultado.Claims.TryGetValue(ClaimProposito, out var proposito)
            || proposito is not string textoProposito)
        {
            return null;
        }

        var paraSesion = resultado.Claims.TryGetValue(ClaimSesion, out var sesion) && sesion as string == "1";
        var correoExterno = resultado.Claims.TryGetValue(ClaimCorreoExterno, out var gml) ? gml as string : null;
        return new DatosDesafio(idDesafio, textoProposito, paraSesion, correoExterno);
    }
}
