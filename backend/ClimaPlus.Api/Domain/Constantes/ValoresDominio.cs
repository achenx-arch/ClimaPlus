namespace ClimaPlus.Api.Domain.Constantes;

// Valores permitidos de los dominios definidos en docs/base-datos/diccionario-datos.md.
// Las restricciones CHECK de la base de datos se construyen a partir de estas constantes
// para que el código y el esquema usen exactamente los mismos textos.

/// <summary>Dominio "Rol" (usuario.rol).</summary>
public static class Roles
{
    public const string Usuario = "USUARIO";
    public const string Administrador = "ADMINISTRADOR";
}

/// <summary>Dominio "Tipo de alerta" (configuracion_alerta.tipo, alerta.tipo).</summary>
public static class TiposAlerta
{
    public const string Lluvia = "LLUVIA";
    public const string TemperaturaAlta = "TEMPERATURA_ALTA";
    public const string TemperaturaBaja = "TEMPERATURA_BAJA";
}

/// <summary>Dominio "Estado de alerta" (alerta.estado).</summary>
public static class EstadosAlerta
{
    public const string Nueva = "NUEVA";
    public const string Leida = "LEIDA";
}

/// <summary>Dominio "Tipo de consulta" (consulta_clima.tipo_consulta).</summary>
public static class TiposConsulta
{
    public const string Actual = "ACTUAL";
    public const string Pronostico = "PRONOSTICO";
}

/// <summary>Dominio "Propósito de desafío" (desafio_autenticacion.proposito). Migración 004.</summary>
public static class PropositosDesafio
{
    public const string VerificarCorreo = "VERIFICAR_CORREO";
    public const string InicioSesion = "INICIO_SESION";
    public const string InicioGoogle = "INICIO_GOOGLE";
    public const string VincularGoogle = "VINCULAR_GOOGLE";
    public const string RecuperarPassword = "RECUPERAR_PASSWORD";
    public const string CambiarPassword = "CAMBIAR_PASSWORD";   // migración 005

    public static readonly string[] Todos = { VerificarCorreo, InicioSesion, InicioGoogle, VincularGoogle, RecuperarPassword, CambiarPassword };
}

/// <summary>Dominio "Estado de desafío" (desafio_autenticacion.estado). Migración 004.</summary>
public static class EstadosDesafio
{
    public const string Pendiente = "PENDIENTE";
    /// <summary>Solo en recuperación: el código fue correcto y falta escribir la contraseña nueva.</summary>
    public const string Verificado = "VERIFICADO";
    public const string Consumido = "CONSUMIDO";
    public const string Anulado = "ANULADO";
}

/// <summary>Dominio "Proveedor de identidad" (identidad_externa.proveedor). Migración 004.</summary>
public static class ProveedoresIdentidad
{
    public const string Google = "GOOGLE";
}
