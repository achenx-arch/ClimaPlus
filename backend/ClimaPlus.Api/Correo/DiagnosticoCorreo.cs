using System.ComponentModel;
using System.Net.Sockets;
using MailKit.Net.Smtp;

namespace ClimaPlus.Api.Correo;

/// <summary>Etapa del envío SMTP en la que ocurrió un fallo.</summary>
public enum EtapaSmtp
{
    Conectar,
    Autenticar,
    Enviar,
    Desconectar,
}

/// <summary>
/// Fallo del envío SMTP con la etapa en la que ocurrió (ConnectAsync, AuthenticateAsync, SendAsync o DisconnectAsync).
/// La excepción original de MailKit se conserva en <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class ErrorEnvioCorreoException : Exception
{
    public ErrorEnvioCorreoException(EtapaSmtp etapa, Exception interna)
        : base($"El envío SMTP falló en la etapa {etapa}.", interna)
    {
        Etapa = etapa;
    }

    public EtapaSmtp Etapa { get; }
}

/// <summary>
/// Describe una excepción del envío de correo con datos técnicos SEGUROS para el log: tipos de la cadena de
/// excepciones (InnerException), códigos SMTP numéricos, códigos de socket y HResult. Nunca incluye el texto de los
/// mensajes (pueden contener la respuesta del servidor, direcciones o la IP del cliente), ni credenciales, códigos de
/// verificación, asuntos o cuerpos.
/// </summary>
public static class DiagnosticoCorreo
{
    private const int MaxNiveles = 6;

    public static EtapaSmtp? Etapa(Exception ex) => (ex as ErrorEnvioCorreoException)?.Etapa;

    /// <summary>Tipo de la excepción útil (la interna si es un <see cref="ErrorEnvioCorreoException"/>).</summary>
    public static string TipoPrincipal(Exception ex) =>
        (ex is ErrorEnvioCorreoException { InnerException: { } interna } ? interna : ex).GetType().Name;

    /// <summary>Ejemplo: "SmtpProtocolException[desconexion_inesperada] &lt;- IOException[hresult=0x80131620] &lt;- SocketException[ConnectionReset]".</summary>
    public static string Describir(Exception ex)
    {
        var partes = new List<string>();
        var actual = ex is ErrorEnvioCorreoException { InnerException: { } interna } ? interna : ex;
        for (var nivel = 0; actual is not null && nivel < MaxNiveles; nivel++, actual = actual.InnerException)
        {
            partes.Add($"{actual.GetType().Name}[{DetalleSeguro(actual)}]");
        }

        return string.Join(" <- ", partes);
    }

    private static string DetalleSeguro(Exception ex) => ex switch
    {
        SmtpCommandException c => $"comando={c.ErrorCode} estado_smtp={(int)c.StatusCode}",
        SmtpProtocolException p => ClasificarProtocolo(p.Message),
        MailKit.Security.SslHandshakeException => "tls_fallido",
        MailKit.Security.AuthenticationException => "credenciales_rechazadas",
        System.Security.Authentication.AuthenticationException => "tls_rechazado_por_el_sistema",
        MailKit.ServiceNotConnectedException => "sin_conexion",
        MailKit.ServiceNotAuthenticatedException => "sin_autenticar",
        SocketException s => $"socket={s.SocketErrorCode}",
        Win32Exception w => $"win32=0x{w.NativeErrorCode:X8}",
        TimeoutException => "tiempo_agotado",
        OperationCanceledException => "cancelado_o_tiempo_agotado",
        _ => $"hresult=0x{ex.HResult:X8}",
    };

    /// <summary>
    /// Los mensajes de SmtpProtocolException los genera MailKit, pero algunos citan la respuesta del servidor; por eso
    /// solo se registra una categoría fija.
    /// </summary>
    private static string ClasificarProtocolo(string mensaje)
    {
        if (mensaje.Contains("unexpectedly disconnected", StringComparison.OrdinalIgnoreCase))
        {
            return "desconexion_inesperada";
        }

        if (mensaje.Contains("parse", StringComparison.OrdinalIgnoreCase) || mensaje.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            return "respuesta_no_valida";
        }

        if (mensaje.Contains("timed out", StringComparison.OrdinalIgnoreCase) || mensaje.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "tiempo_agotado";
        }

        return "otro";
    }
}
