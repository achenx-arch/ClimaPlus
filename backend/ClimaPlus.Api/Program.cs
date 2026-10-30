using ClimaPlus.Api.Data;
using ClimaPlus.Api.Integraciones.OpenWeather;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services;
using ClimaPlus.Api.Services.Alertas;
using ClimaPlus.Api.Services.Clima;
using ClimaPlus.Api.Services.Dispositivos;
using ClimaPlus.Api.Services.Favoritos;
using ClimaPlus.Api.Services.Historial;
using ClimaPlus.Api.Services.Lecturas;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Fase 8 (A05): Kestrel no anuncia su nombre en la cabecera Server y limita el cuerpo de las peticiones a 1 MB.
builder.WebHost.ConfigureKestrel(opciones =>
{
    opciones.AddServerHeader = false;
    opciones.Limits.MaxRequestBodySize = 1_048_576;
});

// Add services to the container.

// Base de datos (Fase 2B).
// La cadena de conexión NO se guarda en archivos versionados:
//   - Desarrollo: dotnet user-secrets set "ConnectionStrings:ClimaPlusDb" "<cadena>"
//   - Otros entornos: variable de entorno ConnectionStrings__ClimaPlusDb
// Se lee al crear el contexto para que las pruebas puedan reemplazar la configuración.
builder.Services.AddDbContext<ClimaPlusDbContext>((servicios, options) =>
    options.UseNpgsql(ObtenerCadenaConexion(servicios.GetRequiredService<IConfiguration>())));

// Seguridad (Fase 2D): JWT, autorización por rol, CORS, rate limiting y PasswordHasher.
builder.Services.AddClimaPlusSeguridad(builder.Configuration);

// Servicios de negocio (Fase 2D).
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UsuarioService>();

// OpenWeather (Fase 2E). La API key solo en User Secrets: OpenWeather:ApiKey.
builder.Services.AddOpenWeather(builder.Configuration);
builder.Services.AddSingleton<CacheBusquedaUbicaciones>();
builder.Services.AddScoped<UbicacionService>();
builder.Services.AddScoped<IRegistroConsultaClima, RegistroConsultaClima>();
builder.Services.AddScoped<ClimaService>();
builder.Services.TryAddSingleton(TimeProvider.System);

// Favoritos (Fase 2F).
builder.Services.AddScoped<FavoritoService>();

// Historial de consultas (Fase 2G).
builder.Services.AddScoped<HistorialService>();

// Configuración de alertas (Fase 2H).
builder.Services.AddScoped<ConfiguracionAlertaService>();

// Alertas generadas: consulta y estado (M09, M15).
builder.Services.AddScoped<AlertaService>();

// Motor de alertas (Fase 2H, RF-16): se ejecuta dentro de GET /api/clima/actual, sin tareas en segundo plano (D-02).
builder.Services.AddScoped<IMotorAlertas, MotorAlertas>();

// Dispositivos ESP32 y lecturas (Fase 7, RF-20 a RF-24). Las lecturas reutilizan el motor de alertas.
builder.Services.AddScoped<DispositivoService>();
builder.Services.AddScoped<LecturaService>();

// Fase 9: estado de la API y de la base de datos para Docker, CI y monitoreo (GET /health).
builder.Services.AddHealthChecks().AddCheck<VerificacionBaseDatos>("postgresql");

// Errores de validación con nombres de campo en camelCase (los mismos del JSON).
builder.Services.AddControllers(opciones =>
    opciones.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()));

// Errores como ProblemDetails (RFC 9457), sin detalles internos.
builder.Services.AddProblemDetails();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opciones =>
{
    opciones.SwaggerDoc("v1", new OpenApiInfo { Title = "ClimaPlus API", Version = "v1" });

    var esquemaJwt = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega solo el token devuelto por /api/auth/login.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    opciones.AddSecurityDefinition("Bearer", esquemaJwt);
    opciones.AddSecurityRequirement(new OpenApiSecurityRequirement { { esquemaJwt, Array.Empty<string>() } });

    // Fase 7: el ESP32 se autentica con X-Device-Key (solo POST /api/lecturas). En Swagger, "Authorize" → DeviceKey.
    var esquemaDispositivo = new OpenApiSecurityScheme
    {
        Name = ApiKeyDispositivo.Cabecera,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API Key del dispositivo (cpk_…), devuelta una sola vez por POST /api/dispositivos.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "DeviceKey" },
    };
    opciones.AddSecurityDefinition("DeviceKey", esquemaDispositivo);
    opciones.AddSecurityRequirement(new OpenApiSecurityRequirement { { esquemaDispositivo, Array.Empty<string>() } });
});

var app = builder.Build();

// Falla al arrancar, con un mensaje claro, si falta la cadena de conexión.
ObtenerCadenaConexion(app.Configuration);

// Configure the HTTP request pipeline.
// Fase 8 (A05): cabeceras de seguridad en todas las respuestas, incluidas las de error.
app.UseCabecerasSeguridad();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HSTS solo fuera de desarrollo: en localhost obligaría al navegador a usar HTTPS en todos los puertos.
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors(PoliticasSeguridad.CorsFrontend);

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// Público: solo responde Healthy/Unhealthy (503), sin detalles.
app.MapHealthChecks("/health").AllowAnonymous();

// Administrador inicial (D-06): solo si AdminInicial está en User Secrets y no existe otro.
await AdminSeeder.EjecutarAsync(app.Services);

app.Run();

static string ObtenerCadenaConexion(IConfiguration configuracion) =>
    configuracion.GetConnectionString("ClimaPlusDb")
    ?? throw new InvalidOperationException(
        "No se encontró la cadena de conexión 'ConnectionStrings:ClimaPlusDb'. " +
        "Configúrala con 'dotnet user-secrets' o con la variable de entorno ConnectionStrings__ClimaPlusDb.");

/// <summary>Expuesta para las pruebas de integración (WebApplicationFactory).</summary>
public partial class Program
{
}
