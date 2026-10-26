using System.Globalization;
using System.Security.Claims;

namespace ClimaPlus.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Obtiene el id del usuario autenticado desde el claim "sub".
    /// Solo se usa en endpoints protegidos, donde el token ya fue validado.
    /// </summary>
    public static long ObtenerIdUsuario(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirst(ClaimsClimaPlus.Sujeto)?.Value;

        if (!long.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            throw new InvalidOperationException("El token no contiene un identificador de usuario válido.");
        }

        return id;
    }
}
