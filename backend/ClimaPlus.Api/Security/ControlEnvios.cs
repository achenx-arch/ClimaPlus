using Microsoft.Extensions.Options;

namespace ClimaPlus.Api.Security;

/// <summary>Resultado de pedir un reenvío de código.</summary>
public enum EstadoReenvio
{
    Permitido,
    Esperar,
    Agotado,
}

/// <summary>
/// Topes en memoria contra el abuso de los correos (condición 2 del diseño aprobado). Una sola instancia de la API,
/// igual que el rate limiting:
/// - Reenvíos por desafío (uno cada 60 s, máximo 5). Se cuentan igual para desafíos reales y señuelos: el
///   comportamiento observable no revela si una cuenta existe.
/// - Correos con código por cuenta (5 por hora sumando todos los propósitos) y avisos por cuenta (1 por hora).
///   Al pasar el tope la API responde igual, pero no envía: evita bombardear un buzón sin revelar nada.
/// - Códigos incorrectos por cuenta en la verificación de correo y la recuperación (10 por hora, por propósito).
/// - Solicitudes de recuperación con envío por cuenta (3 por hora), además del tope general de códigos.
/// </summary>
public sealed class ControlEnvios
{
    private const int EntradasMaximas = 20_000;

    private readonly Dictionary<Guid, (DateTimeOffset Ultimo, int Reenvios)> _reenvios = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _correos = new(StringComparer.Ordinal);
    private readonly object _candado = new();
    private readonly IOptions<CodigosOptions> _opciones;
    private readonly TimeProvider _reloj;

    public ControlEnvios(IOptions<CodigosOptions> opciones, TimeProvider reloj)
    {
        _opciones = opciones;
        _reloj = reloj;
    }

    private static readonly TimeSpan Hora = TimeSpan.FromHours(1);

    /// <summary>Registra el primer envío de un desafío (real o señuelo): desde aquí corre la espera de 60 s.</summary>
    public void RegistrarEmision(Guid idDesafio)
    {
        lock (_candado)
        {
            Depurar();
            _reenvios[idDesafio] = (_reloj.GetUtcNow(), 0);
        }
    }

    /// <summary>
    /// Intenta un reenvío del desafío. Si está permitido, lo cuenta. <paramref name="esperarSegundos"/> indica
    /// cuánto falta (Esperar) o un tiempo prudente antes de volver a empezar (Agotado).
    /// </summary>
    public EstadoReenvio IntentarReenvio(Guid idDesafio, out int esperarSegundos)
    {
        var opciones = _opciones.Value;
        var ahora = _reloj.GetUtcNow();
        lock (_candado)
        {
            var actual = _reenvios.TryGetValue(idDesafio, out var registro) ? registro : (Ultimo: DateTimeOffset.MinValue, Reenvios: 0);
            if (actual.Reenvios >= opciones.MaxReenvios)
            {
                esperarSegundos = opciones.MinutosVerificacion * 60;
                return EstadoReenvio.Agotado;
            }

            var siguiente = actual.Ultimo + TimeSpan.FromSeconds(opciones.SegundosEntreReenvios);
            if (siguiente > ahora)
            {
                esperarSegundos = Math.Max(1, (int)Math.Ceiling((siguiente - ahora).TotalSeconds));
                return EstadoReenvio.Esperar;
            }

            _reenvios[idDesafio] = (ahora, actual.Reenvios + 1);
            esperarSegundos = 0;
            return EstadoReenvio.Permitido;
        }
    }

    /// <summary>true si la cuenta aún puede recibir un correo con código esta hora (y lo cuenta).</summary>
    public bool PermitirCodigo(long idUsuario) => Permitir($"codigo|{idUsuario}", _opciones.Value.MaxCorreosPorHora);

    /// <summary>true si la cuenta aún puede recibir este aviso esta hora (máximo 1, y lo cuenta).</summary>
    public bool PermitirAviso(long idUsuario, string tipo) => Permitir($"aviso|{tipo}|{idUsuario}", 1);

    /// <summary>
    /// true si la cuenta ya acumuló <see cref="CodigosOptions.MaxFallosPorHora"/> códigos incorrectos para ese
    /// propósito en la última hora (verificación de correo y recuperación). No cuenta nada.
    /// </summary>
    public bool FallosAgotados(long idUsuario, string proposito)
    {
        var clave = $"fallo|{proposito}|{idUsuario}";
        var ahora = _reloj.GetUtcNow();
        lock (_candado)
        {
            if (!_correos.TryGetValue(clave, out var fallos))
            {
                return false;
            }

            while (fallos.Count > 0 && ahora - fallos.Peek() >= Hora)
            {
                fallos.Dequeue();
            }

            return fallos.Count >= _opciones.Value.MaxFallosPorHora;
        }
    }

    /// <summary>true si la cuenta aún puede recibir un código de recuperación esta hora (máximo 3, y lo cuenta).</summary>
    public bool PermitirRecuperacion(long idUsuario) =>
        Permitir($"recuperacion|{idUsuario}", _opciones.Value.MaxRecuperacionesPorHora);

    /// <summary>Registra un código incorrecto de la cuenta para ese propósito.</summary>
    public void RegistrarFallo(long idUsuario, string proposito) =>
        Permitir($"fallo|{proposito}|{idUsuario}", int.MaxValue);

    private bool Permitir(string clave, int maximo)
    {
        var ahora = _reloj.GetUtcNow();
        lock (_candado)
        {
            Depurar();
            if (!_correos.TryGetValue(clave, out var envios))
            {
                envios = new Queue<DateTimeOffset>();
                _correos[clave] = envios;
            }

            while (envios.Count > 0 && ahora - envios.Peek() >= Hora)
            {
                envios.Dequeue();
            }

            if (envios.Count >= maximo)
            {
                return false;
            }

            envios.Enqueue(ahora);
            return true;
        }
    }

    // Se llama con el candado tomado: limpia lo que ya no puede afectar a nadie (más de una hora).
    private void Depurar()
    {
        if (_reenvios.Count + _correos.Count < EntradasMaximas)
        {
            return;
        }

        var ahora = _reloj.GetUtcNow();
        foreach (var (id, registro) in _reenvios.Where(r => ahora - r.Value.Ultimo >= Hora).ToList())
        {
            _reenvios.Remove(id);
        }

        foreach (var (clave, envios) in _correos.Where(c => c.Value.Count == 0 || ahora - c.Value.Last() >= Hora).ToList())
        {
            _correos.Remove(clave);
        }
    }
}
