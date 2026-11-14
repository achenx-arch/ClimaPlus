using System.Net;

namespace ClimaPlus.Api.Correo;

/// <summary>
/// Sección "Correo" (variables SMTP_* de Docker Compose). Los valores reales van en User Secrets o en el .env del
/// servidor, nunca en Git ni en Vite.
/// </summary>
public sealed class CorreoOptions
{
    public const string Seccion = "Correo";

    /// <summary>Servidor SMTP (smtp.gmail.com en producción, mailpit en Docker local). Vacío = sin configurar.</summary>
    public string Host { get; set; } = string.Empty;

    public int Puerto { get; set; } = 587;

    public string Usuario { get; set; } = string.Empty;

    /// <summary>En Gmail, una "contraseña de aplicación"; nunca la contraseña de la cuenta.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Dirección del remitente (SMTP_FROM).</summary>
    public string Remitente { get; set; } = string.Empty;

    /// <summary>Nombre visible del remitente (SMTP_FROM_NAME).</summary>
    public string NombreRemitente { get; set; } = "ClimaPlus";

    /// <summary>StartTls (587), SslAlConectar (465) o Ninguna (solo Mailpit o localhost).</summary>
    public string Seguridad { get; set; } = SeguridadCorreo.StartTls;

    public int TimeoutSegundos { get; set; } = 15;

    /// <summary>
    /// Diagnóstico TEMPORAL del SMTP (por defecto false): registra la configuración no secreta, el TLS negociado, las
    /// capacidades y los mecanismos de autenticación del servidor, y el resultado de cada etapa. Nunca registra
    /// credenciales, códigos ni contenido. Activarlo solo mientras se investiga un fallo (Correo__Diagnostico=true).
    /// </summary>
    public bool Diagnostico { get; set; }

    public bool Configurado => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Remitente);

    /// <summary>Servidores que pueden usarse sin TLS: el capturador local Mailpit o la propia máquina.</summary>
    public bool HostEsLocal =>
        string.Equals(Host, "mailpit", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(Host, out var ip) && IPAddress.IsLoopback(ip));

    /// <summary>Errores de configuración; vacío si es válida. Sin TLS solo se acepta un servidor local.</summary>
    public IEnumerable<string> Validar()
    {
        if (!Configurado)
        {
            yield break;
        }

        if (Puerto is < 1 or > 65535)
        {
            yield return "Correo:Puerto debe estar entre 1 y 65535.";
        }

        if (Seguridad is not (SeguridadCorreo.StartTls or SeguridadCorreo.SslAlConectar or SeguridadCorreo.Ninguna))
        {
            yield return $"Correo:Seguridad debe ser {SeguridadCorreo.StartTls}, {SeguridadCorreo.SslAlConectar} o {SeguridadCorreo.Ninguna}.";
        }
        else if (Seguridad == SeguridadCorreo.Ninguna && !HostEsLocal)
        {
            yield return "Correo:Seguridad=Ninguna solo se permite con Mailpit o localhost; un servidor externo exige TLS.";
        }

        if (TimeoutSegundos is < 1 or > 120)
        {
            yield return "Correo:TimeoutSegundos debe estar entre 1 y 120.";
        }

        if (!Remitente.Contains('@', StringComparison.Ordinal) || !MimeKit.MailboxAddress.TryParse(Remitente, out _))
        {
            yield return "Correo:Remitente no es una dirección válida.";
        }
    }
}

public static class SeguridadCorreo
{
    public const string StartTls = "StartTls";
    public const string SslAlConectar = "SslAlConectar";
    public const string Ninguna = "Ninguna";
}
