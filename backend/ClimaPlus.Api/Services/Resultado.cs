namespace ClimaPlus.Api.Services;

public enum TipoError
{
    Validacion,
    Conflicto,
    CredencialesInvalidas,
    CuentaDesactivada,
    NoEncontrado,

    /// <summary>El proveedor externo (OpenWeather) no está disponible o no está configurado (503).</summary>
    ServicioNoDisponible,

    /// <summary>El proveedor externo no respondió a tiempo (504).</summary>
    TiempoAgotado,

    /// <summary>El proveedor externo devolvió datos inválidos (502).</summary>
    RespuestaProveedorInvalida,
}

/// <summary>Error de negocio devuelto por un servicio. Campo = nombre del campo JSON afectado, si aplica.</summary>
public sealed record ErrorServicio(TipoError Tipo, string Mensaje, string? Campo = null);

/// <summary>Resultado de una operación sin valor de retorno.</summary>
public class Resultado
{
    protected Resultado(ErrorServicio? error)
    {
        Error = error;
    }

    public ErrorServicio? Error { get; }

    public bool Exitoso => Error is null;

    public static Resultado Ok() => new(null);

    public static Resultado Falla(ErrorServicio error) => new(error);
}

/// <summary>Resultado de una operación que devuelve un valor.</summary>
public sealed class Resultado<T> : Resultado
{
    private Resultado(T? valor, ErrorServicio? error)
        : base(error)
    {
        Valor = valor;
    }

    public T? Valor { get; }

    public static Resultado<T> Ok(T valor) => new(valor, null);

    public static new Resultado<T> Falla(ErrorServicio error) => new(default, error);
}
