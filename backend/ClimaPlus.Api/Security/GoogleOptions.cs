using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Sección "Google" (autenticación segura, fase 5). Solo el Client ID: el flujo elegido (Google Identity Services +
/// validación del ID token en la API) no usa Client Secret. El Client ID no es secreto (viaja al navegador), pero se
/// configura fuera de Git (user-secrets o GOOGLE_CLIENT_ID) para no mezclar entornos. Vacío = "Continuar con Google"
/// desactivado (503), la API arranca igual.
/// </summary>
public sealed class GoogleOptions
{
    public const string Seccion = "Google";

    /// <summary>Documento de descubrimiento OIDC de Google: de aquí salen el emisor y las claves públicas (JWKS).</summary>
    public const string DocumentoDescubrimiento = "https://accounts.google.com/.well-known/openid-configuration";

    /// <summary>Emisores válidos del ID token según la documentación de Google.</summary>
    public static readonly string[] Emisores = { "https://accounts.google.com", "accounts.google.com" };

    public const string SufijoClientId = ".apps.googleusercontent.com";

    public string ClientId { get; set; } = string.Empty;

    /// <summary>Vigencia del nonce emitido por GET /api/auth/google/iniciar.</summary>
    public int MinutosNonce { get; set; } = 10;

    public bool Configurado => !string.IsNullOrWhiteSpace(ClientId);
}

public static class GoogleServiceCollectionExtensions
{
    /// <summary>
    /// Registra la validación de ID tokens de Google. Las claves públicas se obtienen del documento de descubrimiento
    /// con el ConfigurationManager de Microsoft.IdentityModel (caché y renovación automáticas, solo HTTPS); las
    /// pruebas lo reemplazan por claves de prueba sin salir a Internet.
    /// </summary>
    public static IServiceCollection AddGoogle(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GoogleOptions>()
            .Bind(configuration.GetSection(GoogleOptions.Seccion))
            .Validate(o => !o.Configurado
                           || (o.ClientId.Trim().EndsWith(GoogleOptions.SufijoClientId, StringComparison.Ordinal) && o.ClientId.Length <= 200),
                $"'Google:ClientId' debe terminar en {GoogleOptions.SufijoClientId} (Client ID de tipo \"Aplicación web\" de Google Cloud).")
            .Validate(o => o.MinutosNonce is > 0 and <= 60, "'Google:MinutosNonce' debe estar entre 1 y 60.")
            .ValidateOnStart();

        services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(_ =>
            new ConfigurationManager<OpenIdConnectConfiguration>(
                GoogleOptions.DocumentoDescubrimiento,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true }));
        services.AddSingleton<IValidadorTokenGoogle, ValidadorTokenGoogle>();
        services.AddSingleton<NoncesGoogle>();
        return services;
    }
}
