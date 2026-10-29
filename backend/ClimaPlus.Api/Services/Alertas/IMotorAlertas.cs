using ClimaPlus.Api.DTOs.Alertas;

namespace ClimaPlus.Api.Services.Alertas;

/// <summary>Consulta ACTUAL ya guardada de un usuario autenticado, lista para evaluar (RN-BD-42).</summary>
public sealed record ConsultaActualEvaluable(
    long IdUsuario,
    long IdUbicacion,
    string NombreUbicacion,
    long IdConsulta,
    decimal Temperatura,
    short CodigoCondicion);

/// <summary>Motor de alertas (Fase 2H, RF-16). Se ejecuta al consultar el clima actual (D-02); no hay tareas en segundo plano.</summary>
public interface IMotorAlertas
{
    /// <summary>
    /// Serializa las evaluaciones del mismo usuario hasta el final de la transacción actual
    /// (anti-duplicado con peticiones simultáneas). Debe llamarse dentro de la transacción y
    /// antes de leer las alertas recientes.
    /// </summary>
    Task BloquearUsuarioAsync(long idUsuario, CancellationToken cancelacion);

    /// <summary>Evalúa las reglas, aplica el anti-duplicado de 60 minutos y guarda las alertas nuevas.</summary>
    Task<IReadOnlyList<AlertaGeneradaResponse>> EvaluarConsultaActualAsync(ConsultaActualEvaluable consulta, CancellationToken cancelacion);
}
