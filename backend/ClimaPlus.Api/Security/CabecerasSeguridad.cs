namespace ClimaPlus.Api.Security;

/// <summary>Sección "Seguridad:Cabeceras".</summary>
public sealed class CabecerasSeguridadOptions
{
    public const string Seccion = "Seguridad:Cabeceras";

    /// <summary>Tamaño máximo del cuerpo de una petición (1 MB). La petición más grande de la API mide unos cientos de bytes.</summary>
    public long MaxBytesCuerpo { get; set; } = 1_048_576;
}

/// <summary>
/// Cabeceras de seguridad HTTP de la API (Fase 8, OWASP A05 – Security Misconfiguration; OWASP Secure Headers Project).
/// La API solo devuelve JSON, así que la CSP no permite cargar nada. Swagger UI (solo en desarrollo) necesita sus
/// scripts y estilos, por eso /swagger recibe una CSP propia. Las cabeceras se agregan con OnStarting para que
/// también salgan en las respuestas de error (el manejador de excepciones limpia las cabeceras).
/// </summary>
public static class CabecerasSeguridad
{
    public const string CspApi = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public const string CspSwagger =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; frame-ancestors 'none'";

    public const string PermissionsPolicy = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    public static IApplicationBuilder UseCabecerasSeguridad(this IApplicationBuilder app) =>
        app.Use(async (contexto, siguiente) =>
        {
            var respuesta = contexto.Response;
            var esSwagger = contexto.Request.Path.StartsWithSegments("/swagger");

            respuesta.OnStarting(() =>
            {
                var h = respuesta.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "no-referrer";
                h["Permissions-Policy"] = PermissionsPolicy;
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                h["Content-Security-Policy"] = esSwagger ? CspSwagger : CspApi;

                // Datos personales y tokens: que ningún navegador ni proxy guarde las respuestas de la API.
                if (!esSwagger && !h.ContainsKey("Cache-Control"))
                {
                    h.CacheControl = "no-store";
                }

                h.Remove("Server");
                h.Remove("X-Powered-By");
                return Task.CompletedTask;
            });

            // A04: rechaza cuerpos demasiado grandes antes de leerlos (Kestrel aplica además el mismo límite).
            var maximo = contexto.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<CabecerasSeguridadOptions>>().Value.MaxBytesCuerpo;
            if (contexto.Request.ContentLength > maximo)
            {
                respuesta.StatusCode = StatusCodes.Status413PayloadTooLarge;
                await respuesta.WriteAsJsonAsync(
                    new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = StatusCodes.Status413PayloadTooLarge,
                        Title = "Petición demasiado grande",
                        Detail = "El cuerpo de la petición supera el tamaño permitido.",
                    },
                    (System.Text.Json.JsonSerializerOptions?)null,
                    "application/problem+json");
                return;
            }

            await siguiente(contexto);
        });
}
