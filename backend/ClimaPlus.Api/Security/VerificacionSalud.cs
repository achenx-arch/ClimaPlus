using ClimaPlus.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Fase 9: GET /health para Docker, GitHub Actions y el monitoreo del despliegue. Comprueba que la API responde y que
/// llega a PostgreSQL. Es público y solo devuelve "Healthy" o "Unhealthy": no expone versiones, nombres de servidor ni
/// errores internos (OWASP A05).
/// </summary>
public sealed class VerificacionBaseDatos : IHealthCheck
{
    private readonly ClimaPlusDbContext _db;

    public VerificacionBaseDatos(ClimaPlusDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Sin conexión a la base de datos.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Sin conexión a la base de datos.");
        }
    }
}
