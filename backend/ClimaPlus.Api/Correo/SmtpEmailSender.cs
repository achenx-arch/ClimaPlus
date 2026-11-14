using ClimaPlus.Api.Security;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ClimaPlus.Api.Correo;

/// <summary>
/// Envío por SMTP con MailKit (la librería que Microsoft recomienda en lugar de System.Net.Mail.SmtpClient).
/// Valida el certificado del servidor con las reglas por defecto: no se reemplaza ServerCertificateValidationCallback.
/// Cada fallo se envuelve en <see cref="ErrorEnvioCorreoException"/> con la etapa (Conectar, Autenticar, Enviar o
/// Desconectar) para el diagnóstico. Con Correo:Diagnostico=true registra además datos técnicos seguros de la
/// conexión (TLS negociado, capacidades y mecanismos de autenticación del servidor), nunca credenciales ni contenido.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<CorreoOptions> _opciones;
    private readonly ILogger _log;

    public SmtpEmailSender(IOptionsMonitor<CorreoOptions> opciones)
        : this(opciones, NullLoggerFactory.Instance)
    {
    }

    public SmtpEmailSender(IOptionsMonitor<CorreoOptions> opciones, ILoggerFactory logs)
    {
        _opciones = opciones;
        _log = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion)
    {
        var opciones = _opciones.CurrentValue;
        if (!opciones.Configurado)
        {
            throw new InvalidOperationException("El servidor de correo no está configurado (Correo:Host y Correo:Remitente).");
        }

        var correo = new MimeMessage();
        correo.From.Add(new MailboxAddress(opciones.NombreRemitente, opciones.Remitente));
        correo.To.Add(MailboxAddress.Parse(mensaje.Destinatario));
        correo.Subject = mensaje.Asunto;
        correo.Body = new BodyBuilder { TextBody = mensaje.Texto, HtmlBody = mensaje.Html }.ToMessageBody();

        var seguridad = opciones.Seguridad switch
        {
            SeguridadCorreo.SslAlConectar => SecureSocketOptions.SslOnConnect,
            SeguridadCorreo.Ninguna => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls,
        };

        if (opciones.Diagnostico)
        {
            RegistrarConfiguracion(opciones, mensaje.Tipo);
        }

        using var cliente = new SmtpClient { Timeout = opciones.TimeoutSegundos * 1000 };

        await EtapaAsync(EtapaSmtp.Conectar, () => cliente.ConnectAsync(opciones.Host, opciones.Puerto, seguridad, cancelacion), cancelacion);
        if (opciones.Diagnostico)
        {
            _log.LogInformation(
                "Correo: diagnostico etapa=Conectar resultado=ok seguro={Seguro} tls={Tls} capacidades={Capacidades} autenticacion={Mecanismos}",
                cliente.IsSecure, cliente.SslProtocol, cliente.Capabilities, string.Join(" ", cliente.AuthenticationMechanisms));
        }

        if (!string.IsNullOrEmpty(opciones.Usuario))
        {
            await EtapaAsync(EtapaSmtp.Autenticar, () => cliente.AuthenticateAsync(opciones.Usuario, opciones.Password, cancelacion), cancelacion);
            if (opciones.Diagnostico)
            {
                _log.LogInformation("Correo: diagnostico etapa=Autenticar resultado=ok");
            }
        }

        await EtapaAsync(EtapaSmtp.Enviar, () => cliente.SendAsync(correo, cancelacion), cancelacion);
        if (opciones.Diagnostico)
        {
            _log.LogInformation("Correo: diagnostico etapa=Enviar resultado=ok tipo={Tipo}", mensaje.Tipo);
        }

        await EtapaAsync(EtapaSmtp.Desconectar, () => cliente.DisconnectAsync(true, cancelacion), cancelacion);
    }

    private static async Task EtapaAsync(EtapaSmtp etapa, Func<Task> accion, CancellationToken cancelacion)
    {
        try
        {
            await accion();
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            throw;   // la API se está deteniendo: no es un fallo del SMTP
        }
        catch (Exception ex)
        {
            throw new ErrorEnvioCorreoException(etapa, ex);
        }
    }

    /// <summary>Solo datos no secretos: servidor, puerto, modo TLS y comprobaciones booleanas de la configuración.</summary>
    private void RegistrarConfiguracion(CorreoOptions o, string tipo)
    {
        var combinacionHabitual = (o.Puerto, o.Seguridad) switch
        {
            (465, SeguridadCorreo.SslAlConectar) => true,
            (587, SeguridadCorreo.StartTls) => true,
            (465, _) or (587, _) => false,
            _ => true,
        };

        _log.LogInformation(
            "Correo: diagnostico configuracion tipo={Tipo} host={Host} puerto={Puerto} seguridad={Seguridad} combinacion_puerto_tls_habitual={Combinacion} usuario_definido={UsuarioDefinido} password_definida={PasswordDefinida} password_con_espacios={PasswordConEspacios} usuario_igual_remitente={UsuarioIgualRemitente} timeout_s={Timeout}",
            tipo, o.Host, o.Puerto, o.Seguridad, combinacionHabitual,
            !string.IsNullOrEmpty(o.Usuario), !string.IsNullOrEmpty(o.Password), o.Password.Contains(' ', StringComparison.Ordinal),
            string.Equals(o.Usuario, o.Remitente, StringComparison.OrdinalIgnoreCase), o.TimeoutSegundos);
    }
}
