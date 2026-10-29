namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Reloj de la API en las pruebas: la hora real más un desfase que las pruebas pueden adelantar
/// para simular el paso del tiempo (vigencia de la caché, D-23). Nunca retrocede.
/// </summary>
public sealed class RelojPruebas : TimeProvider
{
    private long _desfaseTicks;

    public TimeSpan Desfase => TimeSpan.FromTicks(Interlocked.Read(ref _desfaseTicks));

    public void Adelantar(TimeSpan tiempo)
    {
        if (tiempo < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tiempo), "El reloj de pruebas no retrocede.");
        }

        Interlocked.Add(ref _desfaseTicks, tiempo.Ticks);
    }

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Desfase;
}
