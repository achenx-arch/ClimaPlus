using ClimaPlus.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Fase 9: GET /health para Docker, GitHub Actions y el monitoreo del despliegue. Comprueba que la API responde, que
/// llega a PostgreSQL y (Fase 7 de la autenticación) que la base tiene todas las migraciones que espera este código:
/// una base sin la 004 hace fallar el inicio de sesión con 500, y así se detecta antes. Es público y solo devuelve
/// "Healthy" o "Unhealthy": no expone versiones, nombres de servidor, migraciones ni errores internos (OWASP A05).
/// Solo lee __EFMigrationsHistory: nunca aplica migraciones.
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
            if (!await _db.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Sin conexión a la base de datos.");
            }

            var pendientes = await MigracionesPendientesAsync(_db, cancellationToken);
            return pendientes.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Faltan migraciones en la base de datos.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Sin conexión a la base de datos.");
        }
    }

    /// <summary>
    /// Migraciones del código que la base todavía no tiene. Vacío con proveedores no relacionales (InMemory de las
    /// pruebas), que no tienen historial de migraciones.
    /// </summary>
    public static async Task<IReadOnlyList<string>> MigracionesPendientesAsync(ClimaPlusDbContext db, CancellationToken cancelacion = default)
    {
        if (!db.Database.IsRelational())
        {
            return Array.Empty<string>();
        }

        return (await db.Database.GetPendingMigrationsAsync(cancelacion)).ToList();
    }

    /// <summary>
    /// Al arrancar, deja en el log qué migraciones faltan (solo sus nombres, nunca la cadena de conexión). No detiene
    /// la API ni aplica nada: aplicar migraciones es un paso manual con respaldo
    /// (docs/despliegue/migracion-004-climaplus_db.md).
    /// </summary>
    public static async Task RegistrarMigracionesPendientesAsync(IServiceProvider servicios, CancellationToken cancelacion = default)
    {
        using var alcance = servicios.CreateScope();
        var log = alcance.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(RegistroSeguridad.Categoria);
        try
        {
            var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
            var pendientes = await MigracionesPendientesAsync(db, cancelacion);
            if (pendientes.Count > 0)
            {
                log.LogCritical(
                    "Base de datos: faltan_migraciones cantidad={Cantidad} migraciones={Migraciones}. Las rutas que usan las tablas nuevas responderán 500 hasta aplicarlas (ver docs/despliegue/migracion-004-climaplus_db.md).",
                    pendientes.Count, string.Join(",", pendientes));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning("Base de datos: no_se_pudo_verificar_migraciones error={Error}", ex.GetType().Name);
        }
    }
}
