using System.Globalization;
using ClimaPlus.Api.Domain.Constantes;

namespace ClimaPlus.Api.Services.Alertas;

/// <summary>Regla de alerta tal como la necesita el evaluador (configuracion_alerta).</summary>
public sealed record ReglaEvaluable(long IdConfiguracion, string Tipo, long? IdUbicacion, decimal? ValorUmbral, bool Activa);

/// <summary>Alerta que corresponde generar, antes del anti-duplicado (RN-BD-24, RN-BD-35).</summary>
public sealed record AlertaPropuesta(string Tipo, decimal? ValorDetectado, decimal? ValorUmbral, string Mensaje);

/// <summary>
/// Evaluación pura de reglas de alerta, sin base de datos (Fase 2H). Reglas aplicadas:
/// RN-BD-22 (solo activas), RN-BD-20 (específica sobre global, por tipo), RN-BD-16 y D-10
/// (ALTA &gt; umbral, BAJA &lt; umbral, LLUVIA con códigos 2xx/3xx/5xx de OpenWeather),
/// RN-BD-24 (valores solo en temperatura) y RN-BD-35 (mensaje de hasta 255 caracteres).
/// </summary>
public static class EvaluadorAlertas
{
    public const int LongitudMaximaMensaje = 255;

    /// <summary>Orden fijo de evaluación y de las alertas devueltas.</summary>
    public static readonly IReadOnlyList<string> OrdenTipos = new[]
    {
        TiposAlerta.TemperaturaAlta,
        TiposAlerta.TemperaturaBaja,
        TiposAlerta.Lluvia,
    };

    /// <summary>
    /// Regla que aplica a cada tipo en la ubicación: primero se descartan las inactivas (RN-BD-22, DP-2)
    /// y después la específica de la ubicación prevalece sobre la global (RN-BD-20). Una regla de otra
    /// ubicación nunca aplica. Una regla global aplica a cualquier ubicación consultada (DP-1).
    /// </summary>
    public static IReadOnlyList<ReglaEvaluable> SeleccionarReglas(IEnumerable<ReglaEvaluable> reglas, long idUbicacion)
    {
        var candidatas = reglas.Where(r => r.Activa && (r.IdUbicacion == idUbicacion || r.IdUbicacion is null)).ToList();
        var seleccionadas = new List<ReglaEvaluable>();

        foreach (var tipo in OrdenTipos)
        {
            var delTipo = candidatas.Where(r => r.Tipo == tipo).ToList();
            var regla = delTipo.FirstOrDefault(r => r.IdUbicacion == idUbicacion) ?? delTipo.FirstOrDefault(r => r.IdUbicacion is null);
            if (regla is not null)
            {
                seleccionadas.Add(regla);
            }
        }

        return seleccionadas;
    }

    /// <summary>Alertas que dispara una medición de clima actual. Si alta y baja se cumplen a la vez, se devuelven ambas (DP-9).</summary>
    public static IReadOnlyList<AlertaPropuesta> Evaluar(
        decimal temperatura, short codigoCondicion, long idUbicacion, string nombreUbicacion, IEnumerable<ReglaEvaluable> reglas) =>
        EvaluarMediciones(temperatura, EsLluvia(codigoCondicion), idUbicacion, nombreUbicacion, reglas);

    /// <summary>
    /// Alertas que dispara una lectura del ESP32 (RF-24, RN-BD-33). Las mediciones son opcionales (RN-BD-31):
    /// sin temperatura no se evalúan las reglas de temperatura y solo <c>lluvia = true</c> dispara LLUVIA (D-08).
    /// Mismas reglas, prioridades y mensajes que la consulta ACTUAL.
    /// </summary>
    public static IReadOnlyList<AlertaPropuesta> EvaluarLectura(
        decimal? temperatura, bool? lluvia, long idUbicacion, string nombreUbicacion, IEnumerable<ReglaEvaluable> reglas) =>
        EvaluarMediciones(temperatura, lluvia == true, idUbicacion, nombreUbicacion, reglas);

    private static IReadOnlyList<AlertaPropuesta> EvaluarMediciones(
        decimal? temperatura, bool llueve, long idUbicacion, string nombreUbicacion, IEnumerable<ReglaEvaluable> reglas)
    {
        var propuestas = new List<AlertaPropuesta>();

        foreach (var regla in SeleccionarReglas(reglas, idUbicacion))
        {
            switch (regla.Tipo)
            {
                case TiposAlerta.TemperaturaAlta when regla.ValorUmbral is { } umbral && temperatura is { } t && t > umbral:
                    propuestas.Add(new AlertaPropuesta(regla.Tipo, t, umbral, MensajeTemperaturaAlta(nombreUbicacion, t, umbral)));
                    break;
                case TiposAlerta.TemperaturaBaja when regla.ValorUmbral is { } umbral && temperatura is { } t && t < umbral:
                    propuestas.Add(new AlertaPropuesta(regla.Tipo, t, umbral, MensajeTemperaturaBaja(nombreUbicacion, t, umbral)));
                    break;
                case TiposAlerta.Lluvia when llueve:
                    propuestas.Add(new AlertaPropuesta(regla.Tipo, null, null, MensajeLluvia(nombreUbicacion)));
                    break;
            }
        }

        return propuestas;
    }

    /// <summary>Grupos de OpenWeather 2xx (tormenta), 3xx (llovizna) y 5xx (lluvia) — diccionario-datos.md.</summary>
    public static bool EsLluvia(short codigoCondicion) => codigoCondicion is >= 200 and <= 399 or >= 500 and <= 599;

    // Formato del mockup M09: "Cobán: 33.4 C supera tu umbral de 32 C" y "Lluvia detectada en Cobán".
    public static string MensajeTemperaturaAlta(string ubicacion, decimal temperatura, decimal umbral) =>
        Recortar($"{ubicacion}: {Numero(temperatura)} C supera tu umbral de {Numero(umbral)} C");

    public static string MensajeTemperaturaBaja(string ubicacion, decimal temperatura, decimal umbral) =>
        Recortar($"{ubicacion}: {Numero(temperatura)} C está por debajo de tu umbral de {Numero(umbral)} C");

    public static string MensajeLluvia(string ubicacion) => Recortar($"Lluvia detectada en {ubicacion}");

    /// <summary>Punto decimal y sin ceros sobrantes: 32, 33.4, -5.25.</summary>
    public static string Numero(decimal valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// RN-BD-35. Con nombres de hasta 100 caracteres (ubicacion.nombre) los mensajes nunca superan
    /// 255; el recorte es solo una defensa para no violar la columna.
    /// </summary>
    private static string Recortar(string mensaje) =>
        mensaje.Length <= LongitudMaximaMensaje ? mensaje : mensaje[..(LongitudMaximaMensaje - 1)] + "…";
}
