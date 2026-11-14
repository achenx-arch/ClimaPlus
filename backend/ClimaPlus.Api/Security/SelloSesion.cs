using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Sello de sesión (OWASP A07, ASVS 3.3.3): el JWT lleva el claim "sv", un HMAC-SHA256 de usuario.sello_seguridad
/// firmado con la clave del JWT. Al validar cada token se recalcula con el sello actual de la base de datos.
/// Cambiar o restablecer la contraseña, o vincular Google, renueva sello_seguridad: todas las sesiones abiertas dejan
/// de ser válidas. Migración 004: antes el sello salía del hash de la contraseña, que no existe en las cuentas solo de
/// Google; por eso al desplegar la 004 todas las sesiones se cierran una vez.
/// El claim no revela nada útil: sin la clave del servidor no se puede relacionar con el sello.
/// </summary>
public static class SelloSesion
{
    public static string Calcular(Guid selloSeguridad, string claveJwt)
    {
        var mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(claveJwt),
            Encoding.UTF8.GetBytes(selloSeguridad.ToString("N", CultureInfo.InvariantCulture)));
        return Convert.ToHexString(mac, 0, 16).ToLowerInvariant();
    }

    /// <summary>Comparación en tiempo constante.</summary>
    public static bool Coincide(string? selloToken, Guid selloSeguridad, string claveJwt)
    {
        if (string.IsNullOrEmpty(selloToken))
        {
            return false;
        }

        var esperado = Encoding.ASCII.GetBytes(Calcular(selloSeguridad, claveJwt));
        var recibido = Encoding.ASCII.GetBytes(selloToken);
        return CryptographicOperations.FixedTimeEquals(esperado, recibido);
    }
}
