using System.Security.Cryptography;
using System.Text;

namespace ClimaPlus.Api.Security;

/// <summary>
/// API Key de los dispositivos ESP32 (07-seguridad-inicial.md, sección 5; RN-BD-28).
/// La clave es aleatoria y de alta entropía (32 bytes), por eso basta un SHA-256 sin sal: permite buscar el
/// dispositivo directamente por <c>api_key_hash</c> (índice único). La clave nunca se guarda ni se registra en logs.
/// </summary>
public static class ApiKeyDispositivo
{
    public const string Cabecera = "X-Device-Key";

    /// <summary>Prefijo legible para reconocer la clave (y para que nadie la confunda con un JWT).</summary>
    public const string Prefijo = "cpk_";

    /// <summary>Más que suficiente para "cpk_" + 43 caracteres; evita hashear cabeceras gigantes.</summary>
    public const int LongitudMaxima = 128;

    private const int BytesAleatorios = 32;

    /// <summary>Genera una clave nueva: "cpk_" + 32 bytes aleatorios en Base64Url (47 caracteres).</summary>
    public static string Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(BytesAleatorios);
        return Prefijo + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>SHA-256 de la clave en hexadecimal minúscula (64 caracteres), igual que <c>dispositivo.api_key_hash</c>.</summary>
    public static string Hash(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant();

    /// <summary>Descarta sin consultar la base de datos las cabeceras vacías, demasiado largas o sin el prefijo.</summary>
    public static bool FormatoValido(string? apiKey) =>
        !string.IsNullOrWhiteSpace(apiKey)
        && apiKey.Length <= LongitudMaxima
        && apiKey.StartsWith(Prefijo, StringComparison.Ordinal);
}
