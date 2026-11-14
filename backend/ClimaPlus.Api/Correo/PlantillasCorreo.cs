using System.Globalization;
using System.Net;
using ClimaPlus.Api.Domain.Constantes;

namespace ClimaPlus.Api.Correo;

/// <summary>
/// Plantillas de los correos de autenticación, en HTML y texto plano. Solo incluyen lo necesario: el código, su
/// vigencia y qué hacer si no fue la persona. Nunca enlaces con tokens, contraseñas, IP ni datos de la cuenta.
/// El nombre del usuario se codifica para HTML (lo escribió el propio usuario al registrarse).
/// </summary>
public static class PlantillasCorreo
{
    public const string Marca = "ClimaPlus";

    // Meses escritos a mano: no depende de que el contenedor tenga los datos de cultura (ICU) instalados.
    private static readonly string[] Meses =
    {
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    };

    /// <summary>Correo con un código de 6 dígitos para el propósito indicado.</summary>
    public static MensajeCorreo Codigo(string destinatario, string nombre, string proposito, string codigo, int minutosVigencia)
    {
        if (codigo.Length != 6 || !codigo.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("El código debe tener 6 dígitos.", nameof(codigo));
        }

        var (asunto, motivo, tipo) = proposito switch
        {
            PropositosDesafio.VerificarCorreo => ("Verifica tu correo en ClimaPlus", "verificar tu correo electrónico", "codigo_verificar_correo"),
            PropositosDesafio.InicioSesion => ("Tu código para iniciar sesión en ClimaPlus", "iniciar sesión", "codigo_inicio_sesion"),
            PropositosDesafio.InicioGoogle => ("Tu código para entrar con Google en ClimaPlus", "terminar de iniciar sesión con Google", "codigo_inicio_google"),
            PropositosDesafio.VincularGoogle => ("Confirma la vinculación de Google en ClimaPlus", "vincular tu cuenta de Google con tu cuenta de ClimaPlus", "codigo_vincular_google"),
            PropositosDesafio.RecuperarPassword => ("Tu código para recuperar la contraseña de ClimaPlus", "restablecer tu contraseña", "codigo_recuperar_password"),
            PropositosDesafio.CambiarPassword => ("Confirma el cambio de contraseña en ClimaPlus", "confirmar el cambio de tu contraseña", "codigo_cambiar_password"),
            _ => throw new ArgumentOutOfRangeException(nameof(proposito), proposito, "Propósito desconocido."),
        };

        var siNoFuisteTu = proposito switch
        {
            PropositosDesafio.RecuperarPassword or PropositosDesafio.InicioSesion =>
                "Si no fuiste tú, ignora este correo y considera cambiar tu contraseña: nadie puede entrar sin este código.",
            PropositosDesafio.CambiarPassword =>
                "Si no fuiste tú, no compartas este código: la contraseña no cambiará sin él. Alguien conoce tu contraseña actual; " +
                "cámbiala cuanto antes con \"¿Olvidaste tu contraseña?\".",
            _ => "Si no fuiste tú, ignora este correo: no se hará ningún cambio.",
        };

        var texto =
            $"Hola, {nombre}:\n\n" +
            $"Usa este código para {motivo}:\n\n" +
            $"    {codigo}\n\n" +
            $"Vence en {minutosVigencia} minutos y solo sirve una vez. Nadie de ClimaPlus te lo pedirá.\n\n" +
            $"{siNoFuisteTu}\n\n— {Marca}";

        var html = Envolver(asunto,
            $"<p style=\"margin:0 0 16px\">Hola, {Html(nombre)}:</p>" +
            $"<p style=\"margin:0 0 16px\">Usa este código para {Html(motivo)}:</p>" +
            $"<p style=\"margin:0 0 16px;font-size:32px;font-weight:700;letter-spacing:8px;font-family:Consolas,Menlo,monospace;color:#0b4f6c\">{codigo}</p>" +
            $"<p style=\"margin:0 0 16px\">Vence en {minutosVigencia} minutos y solo sirve una vez. Nadie de ClimaPlus te lo pedirá.</p>" +
            $"<p style=\"margin:0;color:#555\">{Html(siNoFuisteTu)}</p>");

        return new MensajeCorreo(destinatario, asunto, html, texto, tipo);
    }

    /// <summary>Aviso de seguridad tras cambiar o restablecer la contraseña.</summary>
    public static MensajeCorreo AvisoPasswordCambiada(string destinatario, string nombre, DateTime fechaUtc)
    {
        const string asunto = "Tu contraseña de ClimaPlus cambió";
        var fecha = string.Create(CultureInfo.InvariantCulture,
            $"{fechaUtc.Day} de {Meses[fechaUtc.Month - 1]} de {fechaUtc.Year}, {fechaUtc:HH:mm} UTC");
        var texto =
            $"Hola, {nombre}:\n\n" +
            $"La contraseña de tu cuenta de ClimaPlus se cambió el {fecha}. Por seguridad, se cerraron todas tus sesiones.\n\n" +
            "Si no fuiste tú, usa \"¿Olvidaste tu contraseña?\" en la página de inicio de sesión para recuperar tu cuenta.\n\n" +
            $"— {Marca}";
        var html = Envolver(asunto,
            $"<p style=\"margin:0 0 16px\">Hola, {Html(nombre)}:</p>" +
            $"<p style=\"margin:0 0 16px\">La contraseña de tu cuenta de ClimaPlus se cambió el {Html(fecha)}. Por seguridad, se cerraron todas tus sesiones.</p>" +
            "<p style=\"margin:0;color:#555\">Si no fuiste tú, usa «¿Olvidaste tu contraseña?» en la página de inicio de sesión para recuperar tu cuenta.</p>");
        return new MensajeCorreo(destinatario, asunto, html, texto, "aviso_password_cambiada");
    }

    /// <summary>
    /// Aviso cuando alguien intenta registrarse con un correo que ya tiene cuenta. La respuesta HTTP es la misma que
    /// la de un registro nuevo (no revela que la cuenta existe); solo el dueño del buzón se entera.
    /// </summary>
    public static MensajeCorreo AvisoRegistroExistente(string destinatario, string nombre)
    {
        const string asunto = "Intento de registro con tu correo en ClimaPlus";
        var texto =
            $"Hola, {nombre}:\n\n" +
            "Alguien intentó crear una cuenta de ClimaPlus con este correo, pero ya tienes una.\n\n" +
            "Si fuiste tú, inicia sesión con tu contraseña o usa \"¿Olvidaste tu contraseña?\". Si no fuiste tú, ignora este correo: tu cuenta no cambió.\n\n" +
            $"— {Marca}";
        var html = Envolver(asunto,
            $"<p style=\"margin:0 0 16px\">Hola, {Html(nombre)}:</p>" +
            "<p style=\"margin:0 0 16px\">Alguien intentó crear una cuenta de ClimaPlus con este correo, pero ya tienes una.</p>" +
            "<p style=\"margin:0;color:#555\">Si fuiste tú, inicia sesión con tu contraseña o usa «¿Olvidaste tu contraseña?». Si no fuiste tú, ignora este correo: tu cuenta no cambió.</p>");
        return new MensajeCorreo(destinatario, asunto, html, texto, "aviso_registro_existente");
    }

    /// <summary>Aviso de seguridad cuando se vincula una cuenta de Google.</summary>
    public static MensajeCorreo AvisoGoogleVinculado(string destinatario, string nombre)
    {
        const string asunto = "Vinculaste Google a tu cuenta de ClimaPlus";
        var texto =
            $"Hola, {nombre}:\n\n" +
            "Desde ahora puedes entrar a ClimaPlus con \"Continuar con Google\". Tu contraseña sigue funcionando (con el código por correo de siempre). Por seguridad, se cerraron tus otras sesiones.\n\n" +
            "Si no fuiste tú, cambia tu contraseña y revisa la seguridad de tu cuenta de Google.\n\n" +
            $"— {Marca}";
        var html = Envolver(asunto,
            $"<p style=\"margin:0 0 16px\">Hola, {Html(nombre)}:</p>" +
            "<p style=\"margin:0 0 16px\">Desde ahora puedes entrar a ClimaPlus con «Continuar con Google». Tu contraseña sigue funcionando (con el código por correo de siempre). Por seguridad, se cerraron tus otras sesiones.</p>" +
            "<p style=\"margin:0;color:#555\">Si no fuiste tú, cambia tu contraseña y revisa la seguridad de tu cuenta de Google.</p>");
        return new MensajeCorreo(destinatario, asunto, html, texto, "aviso_google_vinculado");
    }

    public static MensajeCorreo AvisoGoogleDesvinculado(string destinatario, string nombre)
    {
        const string asunto = "Desvinculaste Google de tu cuenta de ClimaPlus";
        var texto =
            $"Hola, {nombre}:\n\n" +
            "Tu cuenta de ClimaPlus ya no acepta \"Continuar con Google\". Sigues entrando con tu correo, tu contraseña y el código por correo. Por seguridad, se cerraron tus otras sesiones.\n\n" +
            "Si no fuiste tú, cambia tu contraseña de inmediato.\n\n" +
            $"— {Marca}";
        var html = Envolver(asunto,
            $"<p style=\"margin:0 0 16px\">Hola, {Html(nombre)}:</p>" +
            "<p style=\"margin:0 0 16px\">Tu cuenta de ClimaPlus ya no acepta «Continuar con Google». Sigues entrando con tu correo, tu contraseña y el código por correo. Por seguridad, se cerraron tus otras sesiones.</p>" +
            "<p style=\"margin:0;color:#555\">Si no fuiste tú, cambia tu contraseña de inmediato.</p>");
        return new MensajeCorreo(destinatario, asunto, html, texto, "aviso_google_desvinculado");
    }

    private static string Html(string texto) => WebUtility.HtmlEncode(texto);

    private static string Envolver(string titulo, string cuerpo) =>
        "<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\">" +
        $"<title>{Html(titulo)}</title></head>" +
        "<body style=\"margin:0;padding:24px;background:#f0f6fa;font-family:Segoe UI,Arial,sans-serif;font-size:15px;line-height:1.5;color:#1b2a33\">" +
        "<div style=\"max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;padding:28px;border:1px solid #d7e4ec\">" +
        "<p style=\"margin:0 0 20px;font-size:20px;font-weight:700;color:#0b4f6c\">ClimaPlus</p>" +
        cuerpo +
        "</div><p style=\"max-width:520px;margin:16px auto 0;font-size:12px;color:#6b7c86;text-align:center\">" +
        "Este es un mensaje automático de ClimaPlus. No respondas a este correo.</p></body></html>";
}
