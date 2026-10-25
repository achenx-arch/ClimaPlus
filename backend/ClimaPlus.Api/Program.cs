using ClimaPlus.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Base de datos (Fase 2B).
// La cadena de conexión NO se guarda en archivos versionados:
//   - Desarrollo: dotnet user-secrets set "ConnectionStrings:ClimaPlusDb" "<cadena>"
//   - Otros entornos: variable de entorno ConnectionStrings__ClimaPlusDb
var connectionString = builder.Configuration.GetConnectionString("ClimaPlusDb")
    ?? throw new InvalidOperationException(
        "No se encontró la cadena de conexión 'ConnectionStrings:ClimaPlusDb'. " +
        "Configúrala con 'dotnet user-secrets' o con la variable de entorno ConnectionStrings__ClimaPlusDb.");

builder.Services.AddDbContext<ClimaPlusDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
