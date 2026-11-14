using System.Threading.Channels;
using ClimaPlus.Api.Security;

namespace ClimaPlus.Api.Correo;

/// <summary>Cola de correos pendientes. Encolar es inmediato: la respuesta HTTP nunca espera al servidor SMTP.</summary>
public interface IColaCorreos
{
    /// <summary>false si la cola está llena (el correo se descarta y queda registrado en el log).</summary>
    bool Encolar(MensajeCorreo mensaje);
}

/// <summary>
/// Cola en memoria con capacidad limitada (una sola instancia de la API, igual que el rate limiting). Si la API se
/// reinicia, los correos pendientes se pierden: el usuario usa "Reenviar código", que genera uno nuevo.
/// </summary>
public sealed class ColaCorreos : IColaCorreos
{
    public const int Capacidad = 1000;

    private readonly Channel<MensajeCorreo> _canal = Channel.CreateBounded<MensajeCorreo>(
        new BoundedChannelOptions(Capacidad) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly ILogger _log;

    public ColaCorreos(ILoggerFactory logs)
    {
        _log = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    public ChannelReader<MensajeCorreo> Lector => _canal.Reader;

    public bool Encolar(MensajeCorreo mensaje)
    {
        if (_canal.Writer.TryWrite(mensaje))
        {
            return true;
        }

        _log.LogError("Correo: cola_llena tipo={Tipo} destino={Destino}", mensaje.Tipo, RegistroSeguridad.EnmascararCorreo(mensaje.Destinatario));
        return false;
    }
}

/// <summary>
/// Envía en segundo plano los correos de <see cref="ColaCorreos"/>, con 3 intentos por correo (esperas de 2 y 10 s).
/// Un fallo del SMTP nunca detiene la API ni deja una cuenta inutilizable: el desafío ya está guardado y el usuario
/// puede pedir otro código. Los logs nunca incluyen el código, el asunto ni el cuerpo.
/// </summary>
public sealed class ServicioEnvioCorreos : BackgroundService
{
    private static readonly TimeSpan[] Esperas = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10) };

    private readonly ColaCorreos _cola;
    private readonly IEmailSender _enviador;
    private readonly ILogger _log;

    public ServicioEnvioCorreos(ColaCorreos cola, IEmailSender enviador, ILoggerFactory logs)
    {
        _cola = cola;
        _enviador = enviador;
        _log = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    /// <summary>Esperas entre intentos; las pruebas las acortan.</summary>
    public TimeSpan[] EsperasEntreIntentos { get; set; } = Esperas;

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        try
        {
            await foreach (var mensaje in _cola.Lector.ReadAllAsync(detener))
            {
                await EnviarConReintentosAsync(mensaje, detener);
            }
        }
        catch (OperationCanceledException) when (detener.IsCancellationRequested)
        {
            // La API se está deteniendo.
        }
    }

    private async Task EnviarConReintentosAsync(MensajeCorreo mensaje, CancellationToken detener)
    {
        var destino = RegistroSeguridad.EnmascararCorreo(mensaje.Destinatario);
        for (var intento = 1; ; intento++)
        {
            try
            {
                await _enviador.EnviarAsync(mensaje, detener);
                _log.LogInformation("Correo: enviado tipo={Tipo} destino={Destino} intento={Intento}", mensaje.Tipo, destino, intento);
                return;
            }
            catch (OperationCanceledException) when (detener.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Etapa, tipos de la cadena de excepciones y códigos numéricos: nunca el texto de los mensajes (MailKit
                // puede citar la respuesta del servidor), ni credenciales, códigos, asunto o cuerpo.
                var hayOtroIntento = intento <= EsperasEntreIntentos.Length;
                var etapa = DiagnosticoCorreo.Etapa(ex)?.ToString() ?? "desconocida";
                _log.LogWarning("Correo: envio_fallido tipo={Tipo} destino={Destino} intento={Intento} etapa={Etapa} error={Error} detalle={Detalle} reintenta={Reintenta}",
                    mensaje.Tipo, destino, intento, etapa, DiagnosticoCorreo.TipoPrincipal(ex), DiagnosticoCorreo.Describir(ex), hayOtroIntento);
                if (!hayOtroIntento)
                {
                    return;
                }

                await Task.Delay(EsperasEntreIntentos[intento - 1], detener);
            }
        }
    }
}
