using ClimaPlus.Api.Domain.Constantes;

namespace ClimaPlus.Api.Domain.Entities;

/// <summary>
/// Tabla <c>desafio_autenticacion</c> (migración 004): un código de 6 dígitos enviado al correo para un propósito
/// concreto (verificar el correo, iniciar sesión, Google o recuperar la contraseña). El código nunca se guarda: solo
/// su HMAC-SHA256 con una clave que no está en la base de datos. Un solo desafío vivo por usuario y propósito.
/// </summary>
public class DesafioAutenticacion
{
    /// <summary>Aleatorio; viaja firmado dentro del token del desafío.</summary>
    public Guid IdDesafio { get; set; }

    public long IdUsuario { get; set; }

    public string Proposito { get; set; } = PropositosDesafio.VerificarCorreo;

    public string Estado { get; set; } = EstadosDesafio.Pendiente;

    /// <summary>HMAC-SHA256 en hexadecimal de "id_desafio:proposito:codigo". NULL solo antes de enviar el código de VINCULAR_GOOGLE.</summary>
    public string? CodigoHash { get; set; }

    public DateTime ExpiraEn { get; set; }

    public short Intentos { get; set; }

    public short Reenvios { get; set; }

    /// <summary>Último envío del código; controla el reenvío (uno cada 60 s).</summary>
    public DateTime? EnviadoEn { get; set; }

    /// <summary>Solo en recuperación: cuándo se verificó el código (el permiso de restablecer dura 10 min).</summary>
    public DateTime? VerificadoEn { get; set; }

    /// <summary>Cuándo pasó a CONSUMIDO o ANULADO.</summary>
    public DateTime? FinalizadoEn { get; set; }

    public DateTime CreadoEn { get; set; }

    /// <summary>El "sub" de Google pendiente de vincular; solo en VINCULAR_GOOGLE.</summary>
    public string? SujetoExterno { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
