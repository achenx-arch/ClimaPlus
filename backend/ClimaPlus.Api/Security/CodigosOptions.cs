using System.Text;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Sección "Codigos" (autenticación segura). La clave HMAC firma los códigos de 6 dígitos guardados en
/// desafio_autenticacion: sin ella, quien robe la base de datos no puede probar el millón de códigos posibles.
/// Debe ser distinta de la clave del JWT y de la contraseña SMTP (variable CODIGOS_CLAVE_HMAC).
/// </summary>
public sealed class CodigosOptions
{
    public const string Seccion = "Codigos";

    public const int BytesMinimosClave = 32;

    public string ClaveHmac { get; set; } = string.Empty;

    /// <summary>Vigencia del código de verificación de correo y de recuperación.</summary>
    public int MinutosVerificacion { get; set; } = 10;

    /// <summary>Vigencia del código de inicio de sesión (y de su token de desafío).</summary>
    public int MinutosInicioSesion { get; set; } = 5;

    /// <summary>Vigencia del código CAMBIAR_PASSWORD del perfil (5 minutos, migración 005).</summary>
    public int MinutosCambioPassword { get; set; } = 5;

    public int MaxIntentos { get; set; } = 5;

    public int SegundosEntreReenvios { get; set; } = 60;

    public int MaxReenvios { get; set; } = 5;

    /// <summary>Correos con código por cuenta y por hora, sumando todos los propósitos.</summary>
    public int MaxCorreosPorHora { get; set; } = 5;

    /// <summary>
    /// Códigos incorrectos por cuenta y por hora en la verificación de correo y la recuperación (solo para ese
    /// propósito). Al llegar al tope, ese propósito deja de aceptar códigos hasta que pase la hora; no afecta al login.
    /// </summary>
    public int MaxFallosPorHora { get; set; } = 10;

    /// <summary>
    /// Solicitudes de recuperación por cuenta y por hora que sí envían código (3). Más allá, la API responde el mismo
    /// 202 pero no envía nada (silencioso): nadie puede llenar el buzón de otra persona.
    /// </summary>
    public int MaxRecuperacionesPorHora { get; set; } = 3;

    /// <summary>Vigencia del permiso de restablecer la contraseña tras validar el código (10 minutos).</summary>
    public int MinutosPermisoRestablecer { get; set; } = 10;

    public bool ClaveValida => Encoding.UTF8.GetByteCount(ClaveHmac) >= BytesMinimosClave;
}

/// <summary>Límite de peticiones por IP para una política (sección RateLimiting:*).</summary>
public abstract class LimiteVentanaOptions
{
    public int PermitLimit { get; set; }

    public int VentanaSegundos { get; set; }
}

/// <summary>POST /api/auth/register: 5 cada 10 minutos por IP.</summary>
public sealed class LimiteRegistroOptions : LimiteVentanaOptions
{
    public const string Seccion = "RateLimiting:Registro";

    public LimiteRegistroOptions()
    {
        PermitLimit = 5;
        VentanaSegundos = 600;
    }
}

/// <summary>Endpoints que validan un código (verificar correo, login, Google, recuperación): 20 por minuto por IP.</summary>
public sealed class LimiteCodigosOptions : LimiteVentanaOptions
{
    public const string Seccion = "RateLimiting:Codigos";

    public LimiteCodigosOptions()
    {
        PermitLimit = 20;
        VentanaSegundos = 60;
    }
}

/// <summary>POST /api/auth/password/solicitar y /api/usuarios/me/password/establecer: 5 cada 15 minutos por IP.</summary>
public sealed class LimiteRecuperacionOptions : LimiteVentanaOptions
{
    public const string Seccion = "RateLimiting:Recuperacion";

    public LimiteRecuperacionOptions()
    {
        PermitLimit = 5;
        VentanaSegundos = 900;
    }
}

/// <summary>POST /api/auth/reenviar-codigo: 10 por hora por IP.</summary>
public sealed class LimiteReenvioOptions : LimiteVentanaOptions
{
    public const string Seccion = "RateLimiting:Reenvio";

    public LimiteReenvioOptions()
    {
        PermitLimit = 10;
        VentanaSegundos = 3600;
    }
}
