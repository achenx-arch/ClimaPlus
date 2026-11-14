using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Doble de prueba de Google: firma ID tokens con una clave RSA de pruebas y publica su clave pública como si fuera
/// el JWKS de Google. La API los valida con su validador real; ninguna prueba sale a Internet ni usa Google Cloud.
/// </summary>
public static class GoogleFalso
{
    public const string ClientId = "climaplus-pruebas.apps.googleusercontent.com";
    public const string Emisor = "https://accounts.google.com";

    /// <summary>"kid" de la clave de prueba. El atacante usa el mismo para que la API busque la clave correcta y falle la firma.</summary>
    public const string IdClave = "google-pruebas-1";

    // Solo se guardan los PARÁMETROS de las claves, generados una vez y de forma completa en el inicializador
    // estático (que .NET ejecuta en un solo hilo). Nunca se comparte un objeto RSA entre hilos: en Windows,
    // RSA.Create(2048) devuelve un RSA que genera la clave de forma perezosa en el primer uso y no es seguro entre
    // hilos; con las pruebas en paralelo, dos hilos llegaban a generar dos claves distintas (la pública publicada
    // no coincidía con la que firmaba → 401) y uno liberaba el identificador del otro (ObjectDisposedException
    // SafeBCryptKeyHandle). Con parámetros, cada RsaSecurityKey crea y libera su propio RSA.
    private static readonly RSAParameters ParametrosGoogle = GenerarParametros();

    /// <summary>Clave que Google no publicó: un token firmado con ella es una falsificación.</summary>
    private static readonly RSAParameters ParametrosAtacante = GenerarParametros();

    private static readonly JsonWebTokenHandler Manejador = new();

    /// <summary>Configuración OIDC con solo la clave pública de prueba.</summary>
    public static IConfigurationManager<OpenIdConnectConfiguration> Configuracion()
    {
        var configuracion = new OpenIdConnectConfiguration { Issuer = Emisor };
        var publica = new RSAParameters { Modulus = ParametrosGoogle.Modulus, Exponent = ParametrosGoogle.Exponent };
        configuracion.SigningKeys.Add(new RsaSecurityKey(publica) { KeyId = IdClave });
        return new StaticConfigurationManager<OpenIdConnectConfiguration>(configuracion);
    }

    /// <summary>Sujeto ("sub") único, con el formato numérico de Google.</summary>
    public static string NuevoSujeto() => "1" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999) + RandomNumberGenerator.GetInt32(100_000, 999_999);

    /// <summary>
    /// ID token como los que entrega Google Identity Services. Cada parámetro permite estropear una parte para
    /// probar que la API la rechaza.
    /// </summary>
    public static string Token(
        string sujeto, string correo, string? nonce, bool correoVerificado = true, string? nombre = "Ana Google",
        string? audiencia = null, string? emisor = null, DateTime? expira = null, bool firmaFalsa = false,
        string algoritmo = SecurityAlgorithms.RsaSha256, string? azp = null, DateTime? ahora = null)
    {
        var emitido = ahora ?? DateTime.UtcNow;
        var claims = new Dictionary<string, object>
        {
            ["sub"] = sujeto,
            ["email"] = correo,
            ["email_verified"] = correoVerificado,
        };
        if (nonce is not null)
        {
            claims["nonce"] = nonce;
        }

        if (nombre is not null)
        {
            claims["name"] = nombre;
        }

        claims["azp"] = azp ?? ClientId;

        return Manejador.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = emisor ?? Emisor,
            Audience = audiencia ?? ClientId,
            IssuedAt = emitido.AddMinutes(-1),
            NotBefore = emitido.AddMinutes(-1),
            Expires = expira ?? emitido.AddHours(1),
            Claims = claims,
            SigningCredentials = new SigningCredentials(ClavePrivada(firmaFalsa ? ParametrosAtacante : ParametrosGoogle), algoritmo),
        });
    }

    /// <summary>
    /// Clave privada nueva para cada token, sin caché de proveedores: ningún objeto criptográfico se comparte entre
    /// pruebas ni con la API.
    /// </summary>
    private static RsaSecurityKey ClavePrivada(RSAParameters parametros) => new(parametros)
    {
        KeyId = IdClave,
        CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
    };

    private static RSAParameters GenerarParametros()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportParameters(includePrivateParameters: true);
    }
}
