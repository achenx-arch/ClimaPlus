using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Registro de la seguridad de la API (Fase 2D): JWT, autorización, CORS,
/// rate limiting y hash de contraseñas. Toda la configuración se lee de forma
/// diferida, para que User Secrets, variables de entorno y las pruebas la puedan reemplazar.
/// </summary>
public static class SeguridadServiceCollectionExtensions
{
    private const string ContenidoProblema = "application/problem+json";

    public static IServiceCollection AddClimaPlusSeguridad(this IServiceCollection services, IConfiguration configuration)
    {
        // --- Opciones validadas al arrancar -------------------------------------------------
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Seccion))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Clave) && Encoding.UTF8.GetByteCount(o.Clave) >= 32,
                "Falta 'Jwt:Clave' o tiene menos de 32 bytes. Configúrala con dotnet user-secrets (nunca en appsettings.json).")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Emisor) && !string.IsNullOrWhiteSpace(o.Audiencia),
                "Faltan 'Jwt:Emisor' o 'Jwt:Audiencia' en la configuración.")
            .Validate(o => o.MinutosExpiracion is > 0 and <= 1440,
                "'Jwt:MinutosExpiracion' debe estar entre 1 y 1440.")
            .ValidateOnStart();

        services.AddOptions<LimiteAutenticacionOptions>()
            .Bind(configuration.GetSection(LimiteAutenticacionOptions.Seccion))
            .Validate(o => o.PermitLimit > 0 && o.VentanaSegundos > 0,
                "'RateLimiting:Autenticacion' requiere PermitLimit y VentanaSegundos mayores que 0.")
            .ValidateOnStart();

        // Autenticación segura (condición 2): políticas por flujo, cada una con su propio límite por IP.
        AgregarLimite<LimiteRegistroOptions>(services, configuration, LimiteRegistroOptions.Seccion);
        AgregarLimite<LimiteCodigosOptions>(services, configuration, LimiteCodigosOptions.Seccion);
        AgregarLimite<LimiteReenvioOptions>(services, configuration, LimiteReenvioOptions.Seccion);
        AgregarLimite<LimiteRecuperacionOptions>(services, configuration, LimiteRecuperacionOptions.Seccion);

        // Códigos de verificación: la clave HMAC es obligatoria, de 32 bytes o más y distinta de la del JWT.
        services.AddOptions<CodigosOptions>()
            .Bind(configuration.GetSection(CodigosOptions.Seccion))
            .Validate(o => o.ClaveValida,
                $"'Codigos:ClaveHmac' es obligatoria y debe tener al menos {CodigosOptions.BytesMinimosClave} bytes (variable CODIGOS_CLAVE_HMAC).")
            .Validate<IOptions<JwtOptions>>((o, jwt) => !string.Equals(o.ClaveHmac, jwt.Value.Clave, StringComparison.Ordinal),
                "'Codigos:ClaveHmac' debe ser distinta de 'Jwt:Clave'.")
            .Validate(o => o.MinutosVerificacion is > 0 and <= 60 && o.MinutosInicioSesion is > 0 and <= 30
                        && o.MinutosCambioPassword is > 0 and <= 30
                        && o.MaxIntentos is > 0 and <= 5 && o.MaxReenvios is >= 0 and <= 5
                        && o.SegundosEntreReenvios > 0 && o.MaxCorreosPorHora > 0 && o.MaxFallosPorHora > 0
                        && o.MaxRecuperacionesPorHora > 0 && o.MinutosPermisoRestablecer > 0,
                "'Codigos' tiene valores fuera de rango (intentos y reenvíos: máximo 5, como en la base de datos).")
            .ValidateOnStart();

        services.AddOptions<LimiteClimaOptions>()
            .Bind(configuration.GetSection(LimiteClimaOptions.Seccion))
            .Validate(o => o.PermitLimit > 0 && o.VentanaSegundos > 0,
                "'RateLimiting:Clima' requiere PermitLimit y VentanaSegundos mayores que 0.")
            .ValidateOnStart();

        services.AddOptions<LimiteDispositivosOptions>()
            .Bind(configuration.GetSection(LimiteDispositivosOptions.Seccion))
            .Validate(o => o.PermitLimit > 0 && o.VentanaSegundos > 0,
                "'RateLimiting:Dispositivos' requiere PermitLimit y VentanaSegundos mayores que 0.")
            .ValidateOnStart();

        services.AddOptions<LimiteGeneralOptions>()
            .Bind(configuration.GetSection(LimiteGeneralOptions.Seccion))
            .Validate(o => o.PermitLimit > 0 && o.VentanaSegundos > 0,
                "'RateLimiting:General' requiere PermitLimit y VentanaSegundos mayores que 0.")
            .ValidateOnStart();

        services.AddOptions<IntentosLoginOptions>()
            .Bind(configuration.GetSection(IntentosLoginOptions.Seccion))
            .Validate(o => o.MaxFallidos > 0 && o.MinutosBloqueo > 0 && o.MaxFallidosCuenta >= o.MaxFallidos,
                "'Seguridad:IntentosLogin' requiere MaxFallidos y MinutosBloqueo mayores que 0, y MaxFallidosCuenta mayor o igual que MaxFallidos.")
            .ValidateOnStart();

        services.AddOptions<CabecerasSeguridadOptions>()
            .Bind(configuration.GetSection(CabecerasSeguridadOptions.Seccion))
            .Validate(o => o.MaxBytesCuerpo > 0, "'Seguridad:Cabeceras:MaxBytesCuerpo' debe ser mayor que 0.")
            .ValidateOnStart();

        // Fase 8 (A05): HSTS de un año fuera de desarrollo (UseHsts en Program.cs).
        services.AddHsts(opciones =>
        {
            opciones.MaxAge = TimeSpan.FromDays(365);
            opciones.IncludeSubDomains = true;
        });

        // --- Servicios de seguridad -------------------------------------------------------
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<LimitadorIntentosLogin>();
        services.AddSingleton<TokenDesafio>();
        services.AddSingleton<ControlEnvios>();

        // --- Autenticación JWT ------------------------------------------------------------
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((opciones, jwtOpciones) =>
            {
                var jwt = jwtOpciones.Value;

                opciones.MapInboundClaims = false;
                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Emisor,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimsClimaPlus.Sujeto,
                    RoleClaimType = ClaimsClimaPlus.Rol,
                };
                opciones.Events = CrearEventosJwt();
            });

        // --- Autorización: todo endpoint es privado salvo [AllowAnonymous] ------------------
        services.AddAuthorization(opciones =>
        {
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // --- CORS: solo los orígenes del frontend configurados -------------------------------
        services.AddCors(opciones =>
        {
            opciones.AddPolicy(PoliticasSeguridad.CorsFrontend, politica =>
            {
                var origenes = configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? Array.Empty<string>();
                politica.WithOrigins(origenes)
                    .WithHeaders("Authorization", "Content-Type")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .WithExposedHeaders("Retry-After");
            });
        });

        // --- Rate limiting (autenticación, clima y dispositivos) ----------------------------------------------
        services.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Fase 8 (A04): límite general por IP para toda la API, además de los límites de cada política.
            opciones.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var limite = http.RequestServices.GetRequiredService<IOptions<LimiteGeneralOptions>>().Value;
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

                return RateLimitPartition.GetFixedWindowLimiter($"{PoliticasSeguridad.RateLimitGeneral}|{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite.PermitLimit,
                    Window = TimeSpan.FromSeconds(limite.VentanaSegundos),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                });
            });
            opciones.OnRejected = async (contexto, cancelacion) =>
            {
                var http = contexto.HttpContext;
                http.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(RegistroSeguridad.Categoria)
                    .LogWarning("Seguridad: rate_limit_excedido ruta={Ruta} ip={Ip}",
                        http.Request.Path.Value, http.Connection.RemoteIpAddress?.ToString() ?? "desconocida");

                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var reintentar))
                {
                    http.Response.Headers["Retry-After"] =
                        ((int)Math.Ceiling(reintentar.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problema = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Demasiadas peticiones",
                    Detail = "Has hecho muchos intentos seguidos. Espera un momento e inténtalo de nuevo.",
                };
                await http.Response.WriteAsJsonAsync(problema, (JsonSerializerOptions?)null, ContenidoProblema, cancelacion);
            };

            opciones.AddPolicy(PoliticasSeguridad.RateLimitAutenticacion, http =>
            {
                var limite = http.RequestServices.GetRequiredService<IOptions<LimiteAutenticacionOptions>>().Value;
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
                var ruta = http.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

                return RateLimitPartition.GetFixedWindowLimiter($"{ip}|{ruta}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite.PermitLimit,
                    Window = TimeSpan.FromSeconds(limite.VentanaSegundos),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                });
            });

            // Autenticación segura (condición 2): registro, validación de códigos y reenvíos, cada uno por IP.
            AgregarPolitica<LimiteRegistroOptions>(opciones, PoliticasSeguridad.RateLimitRegistro);
            AgregarPolitica<LimiteCodigosOptions>(opciones, PoliticasSeguridad.RateLimitCodigos);
            AgregarPolitica<LimiteReenvioOptions>(opciones, PoliticasSeguridad.RateLimitReenvio);
            AgregarPolitica<LimiteRecuperacionOptions>(opciones, PoliticasSeguridad.RateLimitRecuperacion);

            // D-23: /api/clima/* comparte un límite por IP (protege la cuota de OpenWeather).
            opciones.AddPolicy(PoliticasSeguridad.RateLimitClima, http =>
            {
                var limite = http.RequestServices.GetRequiredService<IOptions<LimiteClimaOptions>>().Value;
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

                return RateLimitPartition.GetFixedWindowLimiter($"clima|{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite.PermitLimit,
                    Window = TimeSpan.FromSeconds(limite.VentanaSegundos),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                });
            });

            // Fase 7: POST /api/lecturas, por dispositivo (hash de la API Key, nunca la clave) o por IP.
            opciones.AddPolicy(PoliticasSeguridad.RateLimitDispositivos, http =>
            {
                var limite = http.RequestServices.GetRequiredService<IOptions<LimiteDispositivosOptions>>().Value;
                var apiKey = http.Request.Headers[ApiKeyDispositivo.Cabecera].ToString();
                var particion = ApiKeyDispositivo.FormatoValido(apiKey)
                    ? "dispositivo|" + ApiKeyDispositivo.Hash(apiKey)
                    : "dispositivo-ip|" + (http.Connection.RemoteIpAddress?.ToString() ?? "desconocida");

                return RateLimitPartition.GetFixedWindowLimiter(particion, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite.PermitLimit,
                    Window = TimeSpan.FromSeconds(limite.VentanaSegundos),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                });
            });
        });

        return services;
    }

    private static void AgregarLimite<T>(IServiceCollection services, IConfiguration configuration, string seccion)
        where T : LimiteVentanaOptions
    {
        services.AddOptions<T>()
            .Bind(configuration.GetSection(seccion))
            .Validate(o => o.PermitLimit > 0 && o.VentanaSegundos > 0, $"'{seccion}' requiere PermitLimit y VentanaSegundos mayores que 0.")
            .ValidateOnStart();
    }

    private static void AgregarPolitica<T>(RateLimiterOptions opciones, string politica)
        where T : LimiteVentanaOptions
    {
        opciones.AddPolicy(politica, http =>
        {
            var limite = http.RequestServices.GetRequiredService<IOptions<T>>().Value;
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

            return RateLimitPartition.GetFixedWindowLimiter($"{politica}|{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite.PermitLimit,
                Window = TimeSpan.FromSeconds(limite.VentanaSegundos),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
        });
    }

    private static JwtBearerEvents CrearEventosJwt() => new()
    {
        // 07-seguridad-inicial.md: los tokens de un usuario desactivado deben rechazarse.
        // También se rechaza un token cuyo rol ya no coincide con el de la base de datos.
        OnTokenValidated = async contexto =>
        {
            var servicios = contexto.HttpContext.RequestServices;
            var log = servicios.GetRequiredService<ILoggerFactory>().CreateLogger(RegistroSeguridad.Categoria);
            var sujeto = contexto.Principal?.FindFirst(ClaimsClimaPlus.Sujeto)?.Value;
            var rolToken = contexto.Principal?.FindFirst(ClaimsClimaPlus.Rol)?.Value;

            if (!long.TryParse(sujeto, NumberStyles.None, CultureInfo.InvariantCulture, out var idUsuario))
            {
                log.LogWarning("Seguridad: token_sin_sujeto ip={Ip}", contexto.HttpContext.Connection.RemoteIpAddress?.ToString());
                contexto.Fail("El token no identifica a un usuario.");
                return;
            }

            var db = servicios.GetRequiredService<ClimaPlusDbContext>();
            var estado = await db.Usuarios
                .AsNoTracking()
                .Where(u => u.IdUsuario == idUsuario)
                .Select(u => new { u.Activo, u.Rol, u.SelloSeguridad })
                .SingleOrDefaultAsync(contexto.HttpContext.RequestAborted);

            // Fase 8 (A07) y migración 004: el sello del token debe coincidir con usuario.sello_seguridad; si se renovó
            // (cambio o restablecimiento de contraseña, vinculación con Google), la sesión se revoca.
            var claveJwt = servicios.GetRequiredService<IOptions<JwtOptions>>().Value.Clave;
            var sello = contexto.Principal?.FindFirst(ClaimsClimaPlus.Sello)?.Value;

            string? motivo = estado is null ? "usuario_inexistente"
                : !estado.Activo ? "cuenta_desactivada"
                : !string.Equals(estado.Rol, rolToken, StringComparison.Ordinal) ? "rol_cambiado"
                : !SelloSesion.Coincide(sello, estado.SelloSeguridad, claveJwt) ? "sesion_revocada"
                : null;

            if (motivo is not null)
            {
                log.LogWarning("Seguridad: token_rechazado idUsuario={IdUsuario} motivo={Motivo}", idUsuario, motivo);
                contexto.Fail("La sesión ya no es válida.");
            }
        },

        OnAuthenticationFailed = contexto =>
        {
            // Solo el tipo de error; nunca el token.
            contexto.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(RegistroSeguridad.Categoria)
                .LogInformation("Seguridad: token_invalido tipo={Tipo} ip={Ip}",
                    contexto.Exception.GetType().Name, contexto.HttpContext.Connection.RemoteIpAddress?.ToString());
            return Task.CompletedTask;
        },

        OnChallenge = async contexto =>
        {
            contexto.HandleResponse();
            if (contexto.Response.HasStarted)
            {
                return;
            }

            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            contexto.Response.Headers["WWW-Authenticate"] = "Bearer";
            await contexto.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "No autenticado",
                Detail = "Falta el token de acceso, no es válido o expiró. Inicia sesión de nuevo.",
            }, (JsonSerializerOptions?)null, ContenidoProblema);
        },

        OnForbidden = async contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            await contexto.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Acceso denegado",
                Detail = "Tu rol no tiene permiso para este recurso.",
            }, (JsonSerializerOptions?)null, ContenidoProblema);
        },
    };
}
