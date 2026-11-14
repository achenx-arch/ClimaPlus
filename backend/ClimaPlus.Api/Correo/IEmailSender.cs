namespace ClimaPlus.Api.Correo;

/// <summary>Un correo listo para enviar: siempre con versión HTML y de texto plano.</summary>
/// <param name="Tipo">Clasificación para los logs (por ejemplo "codigo_inicio_sesion"); nunca el contenido.</param>
public sealed record MensajeCorreo(string Destinatario, string Asunto, string Html, string Texto, string Tipo);

/// <summary>
/// Envío de correos (autenticación segura, fase 1). Implementaciones: <see cref="SmtpEmailSender"/> con MailKit
/// (Gmail con contraseña de aplicación en producción, Mailpit en Docker local) y un enviador falso en memoria en las
/// pruebas. Nadie llama a esta interfaz directamente desde un endpoint: los correos pasan por <see cref="IColaCorreos"/>,
/// así la respuesta HTTP no espera al servidor SMTP ni revela con su tiempo si una cuenta existe.
/// </summary>
public interface IEmailSender
{
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion);
}
