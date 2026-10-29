namespace ClimaPlus.Api.Integraciones.OpenWeather;

/// <summary>Tipos de error del proveedor. Nunca incluyen el mensaje original de OpenWeather.</summary>
public enum ErrorProveedor
{
    /// <summary>Falta OpenWeather:ApiKey en la configuración.</summary>
    NoConfigurado,

    /// <summary>OpenWeather respondió 401 o 403 (API key inválida o sin permiso).</summary>
    Autenticacion,

    /// <summary>OpenWeather respondió 404.</summary>
    NoEncontrado,

    /// <summary>OpenWeather respondió 429 (límite de llamadas del plan).</summary>
    LimiteExcedido,

    /// <summary>Error 5xx, otro código inesperado o fallo de red.</summary>
    NoDisponible,

    /// <summary>Se superó el tiempo de espera configurado.</summary>
    TiempoAgotado,

    /// <summary>JSON mal formado, vacío o con datos incompletos o fuera de rango.</summary>
    RespuestaInvalida,
}

public sealed class ResultadoProveedor<T>
    where T : class
{
    private ResultadoProveedor(T? valor, ErrorProveedor? error)
    {
        Valor = valor;
        Error = error;
    }

    public T? Valor { get; }

    public ErrorProveedor? Error { get; }

    public bool Exitoso => Error is null;

    public static ResultadoProveedor<T> Ok(T valor) => new(valor, null);

    public static ResultadoProveedor<T> Falla(ErrorProveedor error) => new(null, error);
}
