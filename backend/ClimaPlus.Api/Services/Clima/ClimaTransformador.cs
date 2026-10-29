using ClimaPlus.Api.Integraciones.OpenWeather.Modelos;
using ClimaPlus.Api.Services.Clima.Modelos;

namespace ClimaPlus.Api.Services.Clima;

/// <summary>
/// Valida y normaliza las respuestas meteorológicas de OpenWeather.
/// Los rangos coinciden con los CHECK de consulta_clima, de modo que un dato normalizado
/// nunca provoca un error de la base de datos. Si falta un dato obligatorio o un valor
/// está fuera de rango, la respuesta completa se rechaza (502, D-24): no se inventan valores.
/// </summary>
public static class ClimaTransformador
{
    public const decimal TemperaturaMin = -90m;
    public const decimal TemperaturaMax = 60m;
    public const decimal SensacionMin = -90m;
    public const decimal SensacionMax = 70m;
    public const decimal VientoMax = 999.99m;      // NUMERIC(5,2)
    public const int CodigoCondicionMin = 200;
    public const int CodigoCondicionMax = 899;
    public const int DescripcionMax = 100;        // VARCHAR(100)
    public const int DiasPronostico = 5;
    public const int BloquesPorDia = 8;

    // ------------------------------------------------------------------ Clima actual

    public static bool IntentarTransformarActual(ClimaActualProveedor? proveedor, out ClimaActualNormalizado? clima, out string? motivo)
    {
        clima = null;
        motivo = null;

        if (proveedor is null)
        {
            motivo = "respuesta_vacia";
            return false;
        }

        var principal = proveedor.Principal;
        var condicion = proveedor.Condiciones?.FirstOrDefault();

        if (!EnRango(principal?.Temperatura, TemperaturaMin, TemperaturaMax, out var temperatura))
        {
            motivo = "temperatura";
            return false;
        }

        if (!EnRango(principal?.SensacionTermica, SensacionMin, SensacionMax, out var sensacion))
        {
            motivo = "sensacion_termica";
            return false;
        }

        if (!Humedad(principal?.Humedad, out var humedad))
        {
            motivo = "humedad";
            return false;
        }

        if (!EnRango(proveedor.Viento?.Velocidad, 0m, VientoMax, out var viento))
        {
            motivo = "velocidad_viento";
            return false;
        }

        if (!Condicion(condicion, out var codigo, out var descripcion))
        {
            motivo = "condicion";
            return false;
        }

        if (proveedor.Fecha is not { } fecha || !UnixValido(fecha))
        {
            motivo = "fecha_observacion";
            return false;
        }

        if (!ZonaHorariaValida(proveedor.ZonaHoraria))
        {
            motivo = "zona_horaria";
            return false;
        }

        var amanecer = proveedor.Sistema?.Amanecer is { } a && UnixValido(a) ? DesdeUnix(a) : (DateTime?)null;
        var atardecer = proveedor.Sistema?.Atardecer is { } t && UnixValido(t) ? DesdeUnix(t) : (DateTime?)null;
        var observado = DesdeUnix(fecha);

        bool esDeDia;
        if (ParteDelDiaPorIcono(condicion!.Icono) is { } porIcono)
        {
            esDeDia = porIcono;
        }
        else if (amanecer is not null && atardecer is not null)
        {
            esDeDia = observado >= amanecer && observado < atardecer;
        }
        else
        {
            motivo = "parte_del_dia";
            return false;
        }

        clima = new ClimaActualNormalizado
        {
            Temperatura = temperatura,
            SensacionTermica = sensacion,
            Humedad = humedad,
            VelocidadViento = viento,
            CodigoCondicion = codigo,
            Descripcion = descripcion,
            Detalle = new DetalleClimaActual
            {
                ObservadoEnUtc = observado,
                ZonaHorariaSegundos = proveedor.ZonaHoraria!.Value,
                EsDeDia = esDeDia,
                TemperaturaMin = Opcional(principal!.TemperaturaMin, ClimaTransformador.TemperaturaMin, ClimaTransformador.TemperaturaMax),
                TemperaturaMax = Opcional(principal.TemperaturaMax, ClimaTransformador.TemperaturaMin, ClimaTransformador.TemperaturaMax),
                PresionHpa = principal.Presion is { } p && p is > 0m and < 2000m ? (int)Math.Round(p, MidpointRounding.AwayFromZero) : null,
                DireccionVientoGrados = proveedor.Viento!.Direccion is { } d && d is >= 0m and <= 360m ? (int)Math.Round(d, MidpointRounding.AwayFromZero) : null,
                AmanecerUtc = amanecer,
                AtardecerUtc = atardecer,
            },
        };
        return true;
    }

    // ------------------------------------------------------------------ Pronóstico

    /// <summary>
    /// D-25: agrupa los bloques de 3 h por fecha local de la ubicación, desde el día local de
    /// <paramref name="ahoraUtc"/>, con un máximo de 5 días.
    /// </summary>
    public static bool IntentarTransformarPronostico(PronosticoProveedor? proveedor, DateTime ahoraUtc, out DetallePronostico? pronostico, out string? motivo)
    {
        pronostico = null;
        motivo = null;

        if (proveedor?.Bloques is not { Count: > 0 } bloquesProveedor)
        {
            motivo = "sin_bloques";
            return false;
        }

        if (!ZonaHorariaValida(proveedor.Ciudad?.ZonaHoraria))
        {
            motivo = "zona_horaria";
            return false;
        }

        var zona = proveedor.Ciudad!.ZonaHoraria!.Value;
        var bloques = new List<BloquePronostico>(bloquesProveedor.Count);

        foreach (var bloque in bloquesProveedor)
        {
            if (!IntentarTransformarBloque(bloque, out var normalizado, out motivo))
            {
                return false;
            }

            bloques.Add(normalizado!);
        }

        var hoyLocal = DateOnly.FromDateTime(DateTime.SpecifyKind(ahoraUtc, DateTimeKind.Utc).AddSeconds(zona));

        var dias = bloques
            .DistinctBy(b => b.FechaHoraUtc)
            .OrderBy(b => b.FechaHoraUtc)
            .GroupBy(b => DateOnly.FromDateTime(b.FechaHoraUtc.AddSeconds(zona)))
            .Where(g => g.Key >= hoyLocal)
            .Take(DiasPronostico)
            .Select(g => CrearDia(g.Key, g.ToList(), zona))
            .ToList();

        if (dias.Count == 0)
        {
            motivo = "sin_dias_vigentes";
            return false;
        }

        pronostico = new DetallePronostico { ZonaHorariaSegundos = zona, Dias = dias };
        return true;
    }

    private static DiaPronostico CrearDia(DateOnly fecha, List<BloquePronostico> bloques, int zona)
    {
        var mediodia = TimeSpan.FromHours(12);
        var representativo = bloques
            .OrderBy(b => (b.FechaHoraUtc.AddSeconds(zona).TimeOfDay - mediodia).Duration())
            .ThenBy(b => b.FechaHoraUtc)
            .First();

        return new DiaPronostico
        {
            Fecha = fecha,
            Completo = bloques.Count == BloquesPorDia,
            TemperaturaMax = bloques.Max(b => b.Temperatura),
            TemperaturaMin = bloques.Min(b => b.Temperatura),
            CodigoCondicion = representativo.CodigoCondicion,
            Descripcion = representativo.Descripcion,
            ProbabilidadLluvia = bloques.Max(b => b.ProbabilidadLluvia),
            Bloques = bloques,
        };
    }

    private static bool IntentarTransformarBloque(BloqueProveedor? bloque, out BloquePronostico? normalizado, out string? motivo)
    {
        normalizado = null;
        motivo = null;

        if (bloque?.Fecha is not { } fecha || !UnixValido(fecha))
        {
            motivo = "bloque_fecha";
            return false;
        }

        if (!EnRango(bloque.Principal?.Temperatura, TemperaturaMin, TemperaturaMax, out var temperatura))
        {
            motivo = "bloque_temperatura";
            return false;
        }

        if (!Humedad(bloque.Principal?.Humedad, out var humedad))
        {
            motivo = "bloque_humedad";
            return false;
        }

        if (!EnRango(bloque.Viento?.Velocidad, 0m, VientoMax, out var viento))
        {
            motivo = "bloque_viento";
            return false;
        }

        var condicion = bloque.Condiciones?.FirstOrDefault();
        if (!Condicion(condicion, out var codigo, out var descripcion))
        {
            motivo = "bloque_condicion";
            return false;
        }

        if (bloque.ProbabilidadPrecipitacion is not { } pop || pop < 0m || pop > 1m)
        {
            motivo = "bloque_probabilidad";
            return false;
        }

        var esDeDia = bloque.ParteDelDia?.Parte switch
        {
            "d" => true,
            "n" => false,
            _ => ParteDelDiaPorIcono(condicion!.Icono),
        };
        if (esDeDia is null)
        {
            motivo = "bloque_parte_del_dia";
            return false;
        }

        normalizado = new BloquePronostico
        {
            FechaHoraUtc = DesdeUnix(fecha),
            Temperatura = temperatura,
            Humedad = humedad,
            VelocidadViento = viento,
            CodigoCondicion = codigo,
            Descripcion = descripcion,
            ProbabilidadLluvia = (int)Math.Round(pop * 100m, MidpointRounding.AwayFromZero),
            EsDeDia = esDeDia.Value,
        };
        return true;
    }

    // ------------------------------------------------------------------ Reglas comunes

    private static bool EnRango(decimal? valor, decimal minimo, decimal maximo, out decimal resultado)
    {
        resultado = 0m;
        if (valor is not { } v)
        {
            return false;
        }

        resultado = Math.Round(v, 2, MidpointRounding.AwayFromZero);
        return resultado >= minimo && resultado <= maximo;
    }

    private static decimal? Opcional(decimal? valor, decimal minimo, decimal maximo) =>
        EnRango(valor, minimo, maximo, out var resultado) ? resultado : null;

    private static bool Humedad(decimal? valor, out short humedad)
    {
        humedad = 0;
        if (valor is not { } v)
        {
            return false;
        }

        var redondeado = Math.Round(v, MidpointRounding.AwayFromZero);
        if (redondeado < 0m || redondeado > 100m)
        {
            return false;
        }

        humedad = (short)redondeado;
        return true;
    }

    private static bool Condicion(CondicionProveedor? condicion, out short codigo, out string descripcion)
    {
        codigo = 0;
        descripcion = string.Empty;

        if (condicion?.Codigo is not { } id || id < CodigoCondicionMin || id > CodigoCondicionMax)
        {
            return false;
        }

        var texto = Normalizacion.Nombre(condicion.Descripcion ?? string.Empty);
        if (texto.Length == 0)
        {
            return false;
        }

        codigo = (short)id;
        descripcion = texto.Length > DescripcionMax ? texto[..DescripcionMax].TrimEnd() : texto;
        return true;
    }

    /// <summary>Iconos de OpenWeather: "10d" = día, "10n" = noche.</summary>
    private static bool? ParteDelDiaPorIcono(string? icono) => icono?.Trim() switch
    {
        { Length: > 0 } i when i.EndsWith('d') => true,
        { Length: > 0 } i when i.EndsWith('n') => false,
        _ => null,
    };

    /// <summary>Los desfases horarios reales están entre UTC−12 y UTC+14.</summary>
    private static bool ZonaHorariaValida(int? zona) => zona is >= -12 * 3600 and <= 14 * 3600;

    /// <summary>Entre 2000-01-01 y 2100-01-01: descarta valores absurdos sin depender del reloj.</summary>
    private static bool UnixValido(long segundos) => segundos is >= 946_684_800 and < 4_102_444_800;

    private static DateTime DesdeUnix(long segundos) => DateTimeOffset.FromUnixTimeSeconds(segundos).UtcDateTime;
}
