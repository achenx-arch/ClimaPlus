using ClimaPlus.Api.Data;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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
});

var app = builder.Build();

// Falla al arrancar, con un mensaje claro, si falta la cadena de conexión.
ObtenerCadenaConexion(app.Configuration);

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Use(async (contexto, siguiente) =>
{
    contexto.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await siguiente(contexto);
});

app.UseCors(PoliticasSeguridad.CorsFrontend);

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

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
