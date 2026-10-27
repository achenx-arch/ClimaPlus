using System.Globalization;
using System.Security.Claims;
using System.Text;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Security;

public sealed record TokenEmitido(string Token, DateTime ExpiraEn);

/// <summary>
/// Emite JWT firmados con HS256. Claims: sub (id_usuario), role (rol) y jti.
/// No incluye correo ni nombre: esos datos se consultan en la base de datos.
/// </summary>
public sealed class JwtTokenService
{
    private readonly IOptions<JwtOptions> _opciones;

    public JwtTokenService(IOptions<JwtOptions> opciones)
    {
        _opciones = opciones;
    }

    public TokenEmitido Crear(Usuario usuario)
    {
        var jwt = _opciones.Value;
        var ahora = DateTime.UtcNow;
        var expira = ahora.AddMinutes(jwt.MinutosExpiracion);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Emisor,
            Audience = jwt.Audiencia,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expira,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimsClimaPlus.Sujeto, usuario.IdUsuario.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimsClimaPlus.Rol, usuario.Rol),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            }),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
                SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new TokenEmitido(token, expira);
    }
}
