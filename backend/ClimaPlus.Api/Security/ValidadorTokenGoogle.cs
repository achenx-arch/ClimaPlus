using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Security;

/// <summary>Datos de la persona según un ID token de Google ya validado.</summary>
/// <param name="Sujeto">"sub": identificador estable de la cuenta de Google. Es lo único que identifica la identidad.</param>
/// <param name="Correo">"email" en minúsculas (dato de contacto; nunca identifica la identidad).</param>
/// <param name="CorreoVerificado">"email_verified": Google comprobó que la persona controla ese correo.</param>
/// <param name="Nombre">"name", si Google lo envió.</param>
/// <param name="Nonce">"nonce" firmado por Google; la API lo compara con los que emitió.</param>
public sealed record IdentidadGoogle(string Sujeto, string Correo, bool CorreoVerificado, string? Nombre, string? Nonce);

/// <summary>Valida el ID token ("credential") que Google Identity Services entrega al navegador.</summary>
public interface IValidadorTokenGoogle
{
    /// <summary>La identidad, o null si el token no es válido (el motivo queda en el log, nunca el token).</summary>
    Task<IdentidadGoogle?> ValidarAsync(string? idToken, CancellationToken cancelacion);
}

/// <summary>
/// Validación del ID token con Microsoft.IdentityModel (sin criptografía manual): firma RS256 con las claves públicas
/// del documento de descubrimiento de Google, emisor (accounts.google.com), audiencia (nuestro Client ID), "azp" si
/// viene, vencimiento con el reloj de la API y "sub" obligatorio. Si la firma usa una clave que aún no está en caché
/// (Google rota sus claves), se pide una renovación y se valida otra vez.
/// </summary>
public sealed class ValidadorTokenGoogle : IValidadorTokenGoogle
{
    /// <summary>Un ID token de Google mide alrededor de 1 KB; se rechaza cualquier cosa desproporcionada.</summary>
    public const int LongitudMaxima = 4096;

    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configuracion;
    private readonly IOptions<GoogleOptions> _opciones;
    private readonly TimeProvider _reloj;
    private readonly ILogger _log;
    private readonly JsonWebTokenHandler _manejador = new() { MapInboundClaims = false };

    public ValidadorTokenGoogle(
        IConfigurationManager<OpenIdConnectConfiguration> configuracion, IOptions<GoogleOptions> opciones,
        TimeProvider reloj, ILoggerFactory logs)
    {
        _configuracion = configuracion;
        _opciones = opciones;
        _reloj = reloj;
        _log = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    public async Task<IdentidadGoogle?> ValidarAsync(string? idToken, CancellationToken cancelacion)
    {
        var clientId = _opciones.Value.ClientId.Trim();
        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > LongitudMaxima || clientId.Length == 0)
        {
            return Rechazar("formato");
        }

        TokenValidationResult resultado;
        try
        {
            resultado = await ValidarFirmaAsync(idToken, clientId, cancelacion);
            if (!resultado.IsValid && resultado.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                _configuracion.RequestRefresh();
                resultado = await ValidarFirmaAsync(idToken, clientId, cancelacion);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException)
        {
            // No se pudieron obtener las claves de Google (red caída, DNS). No se acepta nada sin validar.
            _log.LogError("Seguridad: google_token_rechazado motivo=claves_no_disponibles error={Error}", ex.GetType().Name);
            return null;
        }

        if (!resultado.IsValid)
        {
            return Rechazar(Motivo(resultado.Exception));
        }

        var claims = resultado.Claims;
        var sujeto = Texto(claims, "sub");
        var correo = Texto(claims, "email");
        var azp = Texto(claims, "azp");
        if (string.IsNullOrWhiteSpace(sujeto) || sujeto.Length > 255)
        {
            return Rechazar("sub");
        }

        if (azp is not null && !string.Equals(azp, clientId, StringComparison.Ordinal))
        {
            return Rechazar("azp");
        }

        if (string.IsNullOrWhiteSpace(correo))
        {
            return Rechazar("sin_correo");
        }

        return new IdentidadGoogle(sujeto, correo.Trim().ToLowerInvariant(), EsVerdadero(claims, "email_verified"),
            Texto(claims, "name"), Texto(claims, "nonce"));
    }

    private async Task<TokenValidationResult> ValidarFirmaAsync(string idToken, string clientId, CancellationToken cancelacion)
    {
        var configuracion = await _configuracion.GetConfigurationAsync(cancelacion);
        return await _manejador.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = GoogleOptions.Emisores,
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuracion.SigningKeys,
            RequireSignedTokens = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
            RequireExpirationTime = true,
            ValidateLifetime = true,
            // Reloj de la API (TimeProvider) con 1 minuto de tolerancia por diferencias de hora con Google.
            LifetimeValidator = (_, expira, _, _) => expira is { } fin && fin.AddMinutes(1) > _reloj.GetUtcNow().UtcDateTime,
        });
    }

    private IdentidadGoogle? Rechazar(string motivo)
    {
        _log.LogWarning("Seguridad: google_token_rechazado motivo={Motivo}", motivo);
        return null;
    }

    private static string Motivo(Exception? ex) => ex switch
    {
        SecurityTokenInvalidAudienceException => "audiencia",
        SecurityTokenInvalidIssuerException => "emisor",
        SecurityTokenInvalidLifetimeException or SecurityTokenExpiredException => "vencido",
        SecurityTokenInvalidAlgorithmException => "algoritmo",
        SecurityTokenSignatureKeyNotFoundException or SecurityTokenInvalidSignatureException => "firma",
        _ => "invalido",
    };

    private static string? Texto(IDictionary<string, object> claims, string nombre) =>
        claims.TryGetValue(nombre, out var valor) ? valor as string ?? (valor as JsonElement?)?.ToString() : null;

    private static bool EsVerdadero(IDictionary<string, object> claims, string nombre) =>
        claims.TryGetValue(nombre, out var valor) && valor switch
        {
            bool b => b,
            string s => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
}
