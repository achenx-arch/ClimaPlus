using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ClimaPlus.Api.Security;

/// <summary>Sección "Seguridad:IntentosLogin".</summary>
public sealed class IntentosLoginOptions
{
    public const string Seccion = "Seguridad:IntentosLogin";

    /// <summary>Intentos fallidos permitidos por correo antes del bloqueo temporal.</summary>
    public int MaxFallidos { get; set; } = 5;

    public int MinutosBloqueo { get; set; } = 15;
}

/// <summary>
/// Bloqueo temporal por cuenta (OWASP A07, ASVS 2.2.1). Complementa el rate limiting por IP, que no frena un
/// ataque distribuido contra un mismo correo. Cuenta los fallos por correo normalizado, exista o no la cuenta,
/// para no revelar qué correos están registrados. El bloqueo es temporal y se libera solo (evita que un atacante
/// deje a un usuario sin acceso de forma permanente). En memoria: suficiente para una sola instancia de la API.
/// </summary>
public sealed class LimitadorIntentosLogin
{
    private const int EntradasMaximas = 10_000;

    private readonly ConcurrentDictionary<string, Registro> _registros = new(StringComparer.Ordinal);
    private readonly IOptions<IntentosLoginOptions> _opciones;
    private readonly TimeProvider _reloj;

    public LimitadorIntentosLogin(IOptions<IntentosLoginOptions> opciones, TimeProvider reloj)
    {
        _opciones = opciones;
        _reloj = reloj;
    }

    private TimeSpan Ventana => TimeSpan.FromMinutes(_opciones.Value.MinutosBloqueo);

    /// <summary>Tiempo que falta para poder intentar de nuevo, o null si la cuenta no está bloqueada.</summary>
    public TimeSpan? TiempoBloqueo(string correo)
    {
        if (!_registros.TryGetValue(correo, out var registro))
        {
            return null;
        }

        var ahora = _reloj.GetUtcNow();
        if (ahora - registro.PrimerFallo >= Ventana)
        {
            _registros.TryRemove(correo, out _);
            return null;
        }

        return registro.Fallos >= _opciones.Value.MaxFallidos ? registro.PrimerFallo + Ventana - ahora : null;
    }

    public void RegistrarFallo(string correo)
    {
        var ahora = _reloj.GetUtcNow();
        if (_registros.Count >= EntradasMaximas)
        {
            Depurar(ahora);
        }

        _registros.AddOrUpdate(
            correo,
            _ => new Registro(1, ahora),
            (_, actual) => ahora - actual.PrimerFallo >= Ventana ? new Registro(1, ahora) : actual with { Fallos = actual.Fallos + 1 });
    }

    public void Reiniciar(string correo) => _registros.TryRemove(correo, out _);

    private void Depurar(DateTimeOffset ahora)
    {
        foreach (var (clave, registro) in _registros)
        {
            if (ahora - registro.PrimerFallo >= Ventana)
            {
                _registros.TryRemove(clave, out _);
            }
        }
    }

    private sealed record Registro(int Fallos, DateTimeOffset PrimerFallo);
}
