using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ClimaPlus.Api.Correo;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Enviador de correos en memoria para las pruebas: nunca sale a internet. Guarda cada correo "enviado" y puede
/// simular fallos del servidor SMTP. Los correos llegan por la cola real (<see cref="ColaCorreos"/>) y su servicio en
/// segundo plano, así que las pruebas esperan con <see cref="EsperarAsync"/>.
/// </summary>
public sealed partial class EnviadorCorreoFalso : IEmailSender
{
    // Buzón compartido por todas las fábricas: los correos de prueba son únicos (Guid), así que los ayudantes
    // estáticos (Ayudas) pueden leer el código de un registro sin conocer la fábrica que lo envió.
    private static readonly ConcurrentQueue<MensajeCorreo> Todos = new();

    private readonly ConcurrentQueue<MensajeCorreo> _enviados = new();
    private int _fallosPendientes;
    private int _llamadas;

    public IReadOnlyCollection<MensajeCorreo> Enviados => _enviados.ToArray();

    /// <summary>Total de intentos de envío, incluidos los fallidos.</summary>
    public int Llamadas => Volatile.Read(ref _llamadas);

    /// <summary>Los próximos <paramref name="cantidad"/> envíos fallan como si el SMTP no respondiera.</summary>
    public void FallarProximos(int cantidad) => Interlocked.Exchange(ref _fallosPendientes, cantidad);

    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion)
    {
        Interlocked.Increment(ref _llamadas);
        if (Interlocked.Decrement(ref _fallosPendientes) >= 0)
        {
            throw new IOException("SMTP simulado no disponible");
        }

        Interlocked.Exchange(ref _fallosPendientes, 0);
        _enviados.Enqueue(mensaje);
        Todos.Enqueue(mensaje);
        return Task.CompletedTask;
    }

    /// <summary>Espera (hasta 5 s) un correo que cumpla la condición.</summary>
    public async Task<MensajeCorreo> EsperarAsync(Func<MensajeCorreo, bool> condicion, TimeSpan? limite = null)
    {
        var hasta = DateTime.UtcNow + (limite ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < hasta)
        {
            var encontrado = _enviados.FirstOrDefault(condicion);
            if (encontrado is not null)
            {
                return encontrado;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("No llegó el correo esperado.");
    }

    /// <summary>Correos con código enviados a ese destinatario, en orden.</summary>
    public IReadOnlyList<MensajeCorreo> CodigosPara(string destinatario) =>
        _enviados.Where(m => EsCorreoConCodigo(m) && Mismo(m.Destinatario, destinatario)).ToList();

    /// <summary>Espera el correo con código número <paramref name="numero"/> (1 = el primero) y devuelve sus 6 dígitos.</summary>
    public async Task<string> EsperarCodigoAsync(string destinatario, int numero = 1)
    {
        var hasta = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < hasta)
        {
            var codigos = CodigosPara(destinatario);
            if (codigos.Count >= numero)
            {
                return ExtraerCodigo(codigos[numero - 1]);
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"No llegó el código número {numero}.");
    }

    /// <summary>Igual que <see cref="EsperarCodigoAsync"/> pero en el buzón compartido de todas las fábricas (último código).</summary>
    public static async Task<string> EsperarUltimoCodigoGlobalAsync(string destinatario, int minimo = 1)
    {
        var hasta = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < hasta)
        {
            var codigos = Todos.Where(m => EsCorreoConCodigo(m) && Mismo(m.Destinatario, destinatario)).ToList();
            if (codigos.Count >= minimo)
            {
                return ExtraerCodigo(codigos[^1]);
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("No llegó el correo con el código.");
    }

    public static bool EsCorreoConCodigo(MensajeCorreo mensaje) => mensaje.Tipo.StartsWith("codigo_", StringComparison.Ordinal);

    /// <summary>Los 6 dígitos del texto plano del correo (línea con sangría de 4 espacios).</summary>
    public static string ExtraerCodigo(MensajeCorreo mensaje)
    {
        var coincidencia = LineaCodigo().Match(mensaje.Texto);
        Assert.True(coincidencia.Success, "El correo no contiene un código de 6 dígitos.");
        return coincidencia.Groups[1].Value;
    }

    private static bool Mismo(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^ {4}([0-9]{6})$", RegexOptions.Multiline)]
    private static partial Regex LineaCodigo();
}
