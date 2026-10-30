using System.Security.Cryptography;
using System.Text;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Sello de sesión (OWASP A07, ASVS 3.3.3): el JWT lleva el claim "sv", un HMAC-SHA256 del hash de la contraseña
/// firmado con la clave del JWT. Al validar cada token se recalcula con el hash actual de la base de datos: si la
/// contraseña cambió, todas las sesiones abiertas dejan de ser válidas sin agregar columnas al modelo.
/// El claim no revela nada útil: sin la clave del servidor no se puede relacionar con la contraseña.
/// </summary>
public static class SelloSesion
{
    public static string Calcular(string passwordHash, string claveJwt)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(claveJwt), Encoding.UTF8.GetBytes(passwordHash));
        return Convert.ToHexString(mac, 0, 16).ToLowerInvariant();
    }

    /// <summary>Comparación en tiempo constante.</summary>
    public static bool Coincide(string? selloToken, string passwordHash, string claveJwt)
    {
        if (string.IsNullOrEmpty(selloToken))
        {
            return false;
        }

        var esperado = Encoding.ASCII.GetBytes(Calcular(passwordHash, claveJwt));
        var recibido = Encoding.ASCII.GetBytes(selloToken);
        return CryptographicOperations.FixedTimeEquals(esperado, recibido);
    }
}
