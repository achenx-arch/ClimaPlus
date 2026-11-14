using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ClimaPlus.Api.Security;

/// <summary>Sección "Seguridad:IntentosLogin".</summary>
public sealed class IntentosLoginOptions
{
    public const string Seccion = "Seguridad:IntentosLogin";

    /// <summary>Fallos permitidos para un mismo correo desde una misma IP antes del bloqueo temporal.</summary>
    public int MaxFallidos { get; set; } = 5;

    /// <summary>Fallos permitidos para un mismo correo sumando todas las IP (ataque distribuido).</summary>
    public int MaxFallidosCuenta { get; set; } = 20;

    public int MinutosBloqueo { get; set; } = 15;
}

/// <summary>
/// Bloqueo temporal por cuenta (OWASP A07, ASVS 2.2.1; condición 2 del diseño de autenticación segura). Complementa
/// el rate limiting por IP:
/// - Cuenta + IP: 5 fallos en 15 minutos bloquean ese correo desde esa IP. Un atacante desde una IP no bloquea al
///   dueño, que entra desde otra.
/// - Cuenta (todas las IP): 20 fallos en 15 minutos bloquean el correo desde cualquier IP; un ataque distribuido
///   necesita 4 veces más intentos y aun así queda frenado.
/// Cuentan como fallo una contraseña incorrecta y un código de inicio de sesión incorrecto. Se cuentan por correo
/// normalizado, exista o no la cuenta, para no revelar qué correos están registrados. El bloqueo es temporal y se
/// libera solo. En memoria: suficiente para una sola instancia de la API.
/// </summary>
public sealed class LimitadorIntentosLogin
{
    private const int EntradasMaximas = 20_000;

    private readonly ConcurrentDictionary<string, Registro> _registros = new(StringComparer.Ordinal);
    private readonly IOptions<IntentosLoginOptions> _opciones;
    private readonly TimeProvider _reloj;

    public LimitadorIntentosLogin(IOptions<IntentosLoginOptions> opciones, TimeProvider reloj)
    {
        _opciones = opciones;
        _reloj = reloj;
    }

    private TimeSpan Ventana => TimeSpan.FromMinutes(_opciones.Value.MinutosBloqueo);

    private static string ClaveCuenta(string correo) => "c|" + correo;

    private static string ClaveCuentaIp(string correo, string ip) => $"ci|{ip}|{correo}";

    /// <summary>Tiempo que falta para poder intentar de nuevo con ese correo desde esa IP, o null si no hay bloqueo.</summary>
    public TimeSpan? TiempoBloqueo(string correo, string ip)
    {
        var ahora = _reloj.GetUtcNow();
        var porIp = Bloqueo(ClaveCuentaIp(correo, ip), _opciones.Value.MaxFallidos, ahora);
        var porCuenta = Bloqueo(ClaveCuenta(correo), _opciones.Value.MaxFallidosCuenta, ahora);

        if (porIp is null)
        {
            return porCuenta;
        }

        return porCuenta is null || porIp > porCuenta ? porIp : porCuenta;
    }

    /// <summary>Registra un fallo (contraseña o código) del correo desde la IP: suma a los dos contadores.</summary>
    public void RegistrarFallo(string correo, string ip)
    {
        var ahora = _reloj.GetUtcNow();
        if (_registros.Count >= EntradasMaximas)
        {
            Depurar(ahora);
        }

        Sumar(ClaveCuentaIp(correo, ip), ahora);
        Sumar(ClaveCuenta(correo), ahora);
    }

    /// <summary>
    /// Inicio de sesión completo (contraseña y código): se reinicia el contador de esa IP. El global de la cuenta
    /// no se reinicia: vence solo, así un atacante con la contraseña no puede borrar su rastro.
    /// </summary>
    public void Reiniciar(string correo, string ip) => _registros.TryRemove(ClaveCuentaIp(correo, ip), out _);

    private TimeSpan? Bloqueo(string clave, int maximo, DateTimeOffset ahora)
    {
        if (!_registros.TryGetValue(clave, out var registro))
        {
            return null;
        }

        if (ahora - registro.PrimerFallo >= Ventana)
        {
            _registros.TryRemove(clave, out _);
            return null;
        }

        return registro.Fallos >= maximo ? registro.PrimerFallo + Ventana - ahora : null;
    }

    private void Sumar(string clave, DateTimeOffset ahora) =>
        _registros.AddOrUpdate(
            clave,
            _ => new Registro(1, ahora),
            (_, actual) => ahora - actual.PrimerFallo >= Ventana ? new Registro(1, ahora) : actual with { Fallos = actual.Fallos + 1 });

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
