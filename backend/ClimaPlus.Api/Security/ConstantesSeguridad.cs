namespace ClimaPlus.Api.Security;

/// <summary>Nombres de claims del JWT de ClimaPlus.</summary>
public static class ClaimsClimaPlus
{
    /// <summary>Identificador del usuario (id_usuario).</summary>
    public const string Sujeto = "sub";

    /// <summary>Rol del usuario (USUARIO o ADMINISTRADOR).</summary>
    public const string Rol = "role";

    /// <summary>Sello de sesión: cambia cuando se renueva usuario.sello_seguridad (ver SelloSesion).</summary>
    public const string Sello = "sv";
}

/// <summary>Nombres de políticas registradas en la aplicación.</summary>
public static class PoliticasSeguridad
{
    public const string CorsFrontend = "FrontendClimaPlus";

    public const string RateLimitAutenticacion = "autenticacion";

    /// <summary>Autenticación segura: POST /api/auth/register, por IP.</summary>
    public const string RateLimitRegistro = "registro";

    /// <summary>Autenticación segura: endpoints que validan un código de 6 dígitos, por IP (límite compartido).</summary>
    public const string RateLimitCodigos = "codigos";

    /// <summary>Autenticación segura: POST /api/auth/reenviar-codigo, por IP.</summary>
    public const string RateLimitReenvio = "reenvio";

    /// <summary>Solicitar recuperación o establecer contraseña (envían un código al correo).</summary>
    public const string RateLimitRecuperacion = "recuperacion";

    public const string RateLimitClima = "clima";

    public const string RateLimitDispositivos = "dispositivos";

    public const string RateLimitGeneral = "general";
}

/// <summary>Categoría de log para eventos de seguridad. Nunca registrar contraseñas ni tokens.</summary>
public static class RegistroSeguridad
{
    public const string Categoria = "ClimaPlus.Seguridad";

    /// <summary>Devuelve el correo enmascarado para los logs: "a***@dominio.com".</summary>
    public static string EnmascararCorreo(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo))
        {
            return "(vacío)";
        }

        var arroba = correo.IndexOf('@');
        if (arroba <= 0)
        {
            return "***";
        }

        return string.Concat(correo.AsSpan(0, 1), "***", correo.AsSpan(arroba));
    }
}
