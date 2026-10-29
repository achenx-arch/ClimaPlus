using System.Text.Json;
using System.Text.Json.Nodes;
using ClimaPlus.Api.Integraciones.OpenWeather.Modelos;
using ClimaPlus.Api.Services.Clima;
using ClimaPlus.Api.Services.Clima.Modelos;
using ClimaPlus.Api.Tests.Infraestructura;

namespace ClimaPlus.Api.Tests.Clima;

/// <summary>Pruebas unitarias de la validación y normalización de clima actual y pronóstico (Fase 2E, D-25).</summary>
public sealed class ClimaTransformadorTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly DateTime InicioPronostico = new(2026, 10, 27, 21, 0, 0, DateTimeKind.Utc);   // 15:00 en Cobán
    private static readonly DateTime Ahora = new(2026, 10, 27, 20, 0, 0, DateTimeKind.Utc);              // 14:00 en Cobán

    private static T Leer<T>(JsonNode nodo) => nodo.Deserialize<T>(Web)!;

    // ------------------------------------------------------------------ Clima actual

    [Fact]
    public void Actual_Valido_SeNormaliza()
    {
        var ok = ClimaTransformador.IntentarTransformarActual(Leer<ClimaActualProveedor>(RespuestasOpenWeather.ClimaActual()), out var clima, out var motivo);

        Assert.True(ok, motivo);
        Assert.Equal(22.46m, clima!.Temperatura);           // 2 decimales
        Assert.Equal(22.1m, clima.SensacionTermica);
        Assert.Equal((short)70, clima.Humedad);
        Assert.Equal(3.1m, clima.VelocidadViento);
        Assert.Equal((short)802, clima.CodigoCondicion);
        Assert.Equal("nubes dispersas", clima.Descripcion);

        var d = clima.Detalle;
        Assert.Equal(1, d.Version);
        Assert.Equal(new DateTime(2026, 10, 27, 18, 20, 0, DateTimeKind.Utc), d.ObservadoEnUtc);
        Assert.Equal(DateTimeKind.Utc, d.ObservadoEnUtc.Kind);
        Assert.Equal(-21600, d.ZonaHorariaSegundos);
        Assert.True(d.EsDeDia);
        Assert.Equal(16m, d.TemperaturaMin);
        Assert.Equal(25m, d.TemperaturaMax);
        Assert.Equal(1018, d.PresionHpa);
        Assert.Equal(45, d.DireccionVientoGrados);
        Assert.Equal(new DateTime(2026, 10, 27, 11, 58, 0, DateTimeKind.Utc), d.AmanecerUtc);
        Assert.Equal(new DateTime(2026, 10, 27, 23, 41, 0, DateTimeKind.Utc), d.AtardecerUtc);
    }

    [Theory]
    [InlineData("sin_main", "temperatura")]
    [InlineData("temperatura_70", "temperatura")]
    [InlineData("sensacion_75", "sensacion_termica")]
    [InlineData("humedad_150", "humedad")]
    [InlineData("humedad_negativa", "humedad")]
    [InlineData("viento_negativo", "velocidad_viento")]
    [InlineData("viento_1000", "velocidad_viento")]
    [InlineData("sin_viento", "velocidad_viento")]
    [InlineData("codigo_100", "condicion")]
    [InlineData("codigo_900", "condicion")]
    [InlineData("sin_condicion", "condicion")]
    [InlineData("descripcion_vacia", "condicion")]
    [InlineData("sin_fecha", "fecha_observacion")]
    [InlineData("sin_zona_horaria", "zona_horaria")]
    [InlineData("zona_horaria_absurda", "zona_horaria")]
    [InlineData("sin_icono_ni_sol", "parte_del_dia")]
    public void Actual_DatosInvalidos_SeRechaza(string caso, string motivoEsperado)
    {
        var json = RespuestasOpenWeather.ClimaActual();
        var main = json["main"]!.AsObject();
        var condicion = json["weather"]![0]!.AsObject();
        switch (caso)
        {
            case "sin_main": json.Remove("main"); break;
            case "temperatura_70": main["temp"] = 70m; break;
            case "sensacion_75": main["feels_like"] = 75m; break;
            case "humedad_150": main["humidity"] = 150; break;
            case "humedad_negativa": main["humidity"] = -1; break;
            case "viento_negativo": json["wind"]!["speed"] = -0.5m; break;
            case "viento_1000": json["wind"]!["speed"] = 1000m; break;
            case "sin_viento": json.Remove("wind"); break;
            case "codigo_100": condicion["id"] = 100; break;
            case "codigo_900": condicion["id"] = 900; break;
            case "sin_condicion": json["weather"] = new JsonArray(); break;
            case "descripcion_vacia": condicion["description"] = "   "; break;
            case "sin_fecha": json.Remove("dt"); break;
            case "sin_zona_horaria": json.Remove("timezone"); break;
            case "zona_horaria_absurda": json["timezone"] = 90000; break;
            case "sin_icono_ni_sol": condicion.Remove("icon"); json.Remove("sys"); break;
        }

        var ok = ClimaTransformador.IntentarTransformarActual(Leer<ClimaActualProveedor>(json), out var clima, out var motivo);

        Assert.False(ok);
        Assert.Null(clima);
        Assert.Equal(motivoEsperado, motivo);
    }

    [Fact]
    public void Actual_DescripcionLarga_SeRecortaA100YSinIconoUsaAmanecerYAtardecer()
    {
        var json = RespuestasOpenWeather.ClimaActual();
        var condicion = json["weather"]![0]!.AsObject();
        condicion["description"] = "  lluvia   " + new string('x', 150);
        condicion.Remove("icon");

        var ok = ClimaTransformador.IntentarTransformarActual(Leer<ClimaActualProveedor>(json), out var clima, out var motivo);

        Assert.True(ok, motivo);
        Assert.Equal(100, clima!.Descripcion.Length);
        Assert.StartsWith("lluvia x", clima.Descripcion);
        Assert.True(clima.Detalle.EsDeDia);   // 18:20Z está entre amanecer (11:58Z) y atardecer (23:41Z)
    }

    [Fact]
    public void Actual_ValoresOpcionalesFueraDeRango_QuedanNull()
    {
        var json = RespuestasOpenWeather.ClimaActual();
        json["main"]!["pressure"] = 0;
        json["wind"]!["deg"] = 400;
        json["main"]!.AsObject().Remove("temp_min");

        var ok = ClimaTransformador.IntentarTransformarActual(Leer<ClimaActualProveedor>(json), out var clima, out _);

        Assert.True(ok);
        Assert.Null(clima!.Detalle.PresionHpa);
        Assert.Null(clima.Detalle.DireccionVientoGrados);
        Assert.Null(clima.Detalle.TemperaturaMin);
    }

    // ------------------------------------------------------------------ Pronóstico

    [Fact]
    public void Pronostico_AgrupaPorFechaLocalYLimitaACincoDias()
    {
        var ok = ClimaTransformador.IntentarTransformarPronostico(
            Leer<PronosticoProveedor>(RespuestasOpenWeather.Pronostico(InicioPronostico)), Ahora, out var pronostico, out var motivo);

        Assert.True(ok, motivo);
        Assert.Equal(-21600, pronostico!.ZonaHorariaSegundos);
        Assert.Equal(
            new[] { new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 31) },
            pronostico.Dias.Select(d => d.Fecha));

        // Hoy: solo quedan 15:00, 18:00 y 21:00 locales; el 1 de noviembre se descarta (sexto día).
        Assert.False(pronostico.Dias[0].Completo);
        Assert.Equal(3, pronostico.Dias[0].Bloques.Count);
        Assert.All(pronostico.Dias.Skip(1), d => Assert.True(d.Completo));
        Assert.All(pronostico.Dias.Skip(1), d => Assert.Equal(8, d.Bloques.Count));
    }

    [Fact]
    public void Pronostico_CalculaMaximaMinimaCondicionDelMediodiaYLluviaMaxima()
    {
        ClimaTransformador.IntentarTransformarPronostico(
            Leer<PronosticoProveedor>(RespuestasOpenWeather.Pronostico(InicioPronostico)), Ahora, out var pronostico, out _);

        // 28/10 local = bloques 3..10. temp = 15 + i % 8 → 18,19,20,21,22,15,16,17.
        var dia = pronostico!.Dias[1];
        Assert.Equal(22m, dia.TemperaturaMax);
        Assert.Equal(15m, dia.TemperaturaMin);

        // Mediodía local del 28 = 18:00Z = bloque 7 → condición 800 + 7 % 4 = 803.
        Assert.Equal((short)803, dia.CodigoCondicion);
        Assert.Equal("condicion 803", dia.Descripcion);

        // pop = (i % 5) / 10 → máximo 0,4 → 40 %.
        Assert.Equal(40, dia.ProbabilidadLluvia);

        // Hoy (15:00, 18:00, 21:00 locales): el más cercano al mediodía es 15:00 = bloque 0.
        Assert.Equal((short)800, pronostico.Dias[0].CodigoCondicion);

        var bloque = pronostico.Dias[1].Bloques[0];
        Assert.Equal(new DateTime(2026, 10, 28, 6, 0, 0, DateTimeKind.Utc), bloque.FechaHoraUtc);   // 00:00 local
        Assert.False(bloque.EsDeDia);
        Assert.Equal(30, bloque.ProbabilidadLluvia);
    }

    [Fact]
    public void Pronostico_BloquesDuplicadosODesordenados_SeOrdenanYSeEliminan()
    {
        var json = RespuestasOpenWeather.Pronostico(InicioPronostico);
        var lista = json["list"]!.AsArray();
        var primero = lista[0]!.DeepClone();
        var ultimo = lista[^1]!.DeepClone();
        lista.RemoveAt(lista.Count - 1);
        lista.Insert(0, ultimo);
        lista.Add(primero);

        var ok = ClimaTransformador.IntentarTransformarPronostico(Leer<PronosticoProveedor>(json), Ahora, out var pronostico, out _);

        Assert.True(ok);
        Assert.Equal(3, pronostico!.Dias[0].Bloques.Count);
        var fechas = pronostico.Dias.SelectMany(d => d.Bloques).Select(b => b.FechaHoraUtc).ToList();
        Assert.Equal(fechas.OrderBy(f => f).ToList(), fechas);
        Assert.Equal(fechas.Count, fechas.Distinct().Count());
    }

    [Theory]
    [InlineData("bloque_humedad")]
    [InlineData("bloque_probabilidad")]
    [InlineData("bloque_temperatura")]
    [InlineData("bloque_condicion")]
    [InlineData("zona_horaria")]
    [InlineData("sin_bloques")]
    public void Pronostico_DatoInvalido_RechazaTodoElPronostico(string caso)
    {
        var json = RespuestasOpenWeather.Pronostico(InicioPronostico);
        var bloque = json["list"]![10]!.AsObject();
        switch (caso)
        {
            case "bloque_humedad": bloque["main"]!["humidity"] = 150; break;
            case "bloque_probabilidad": bloque["pop"] = 1.5m; break;
            case "bloque_temperatura": bloque["main"]!.AsObject().Remove("temp"); break;
            case "bloque_condicion": bloque["weather"]![0]!["id"] = 99; break;
            case "zona_horaria": json["city"]!.AsObject().Remove("timezone"); break;
            case "sin_bloques": json["list"] = new JsonArray(); break;
        }

        var ok = ClimaTransformador.IntentarTransformarPronostico(Leer<PronosticoProveedor>(json), Ahora, out var pronostico, out var motivo);

        Assert.False(ok);
        Assert.Null(pronostico);
        Assert.Equal(caso, motivo);
    }

    [Fact]
    public void Pronostico_ZonaHorariaPositiva_CambiaElDiaLocal()
    {
        // UTC+9: 21:00Z del 27 ya es 06:00 del 28 en la ubicación.
        var ok = ClimaTransformador.IntentarTransformarPronostico(
            Leer<PronosticoProveedor>(RespuestasOpenWeather.Pronostico(InicioPronostico, 40, 9 * 3600)), Ahora, out var pronostico, out _);

        Assert.True(ok);
        Assert.Equal(new DateOnly(2026, 10, 28), pronostico!.Dias[0].Fecha);
        Assert.Equal(6, pronostico.Dias[0].Bloques.Count);   // 06:00 a 21:00 locales
    }

    // ------------------------------------------------------------------ Formato de detalle (sección 6 de E-02/E-03)

    [Fact]
    public void Detalle_SeSerializaConLasClavesAcordadas()
    {
        ClimaTransformador.IntentarTransformarActual(Leer<ClimaActualProveedor>(RespuestasOpenWeather.ClimaActual()), out var clima, out _);
        ClimaTransformador.IntentarTransformarPronostico(
            Leer<PronosticoProveedor>(RespuestasOpenWeather.Pronostico(InicioPronostico)), Ahora, out var pronostico, out _);

        using var actual = JsonDocument.Parse(DetalleSerializador.Serializar(clima!.Detalle));
        Assert.Equal(
            new[] { "version", "observadoEnUtc", "zonaHorariaSegundos", "esDeDia", "temperaturaMin", "temperaturaMax", "presionHpa", "direccionVientoGrados", "amanecerUtc", "atardecerUtc" },
            actual.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, actual.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("2026-10-27T18:20:00Z", actual.RootElement.GetProperty("observadoEnUtc").GetString());

        using var doc = JsonDocument.Parse(DetalleSerializador.Serializar(pronostico!));
        var raiz = doc.RootElement;
        Assert.Equal(new[] { "version", "zonaHorariaSegundos", "dias" }, raiz.EnumerateObject().Select(p => p.Name));
        var dia = raiz.GetProperty("dias")[0];
        Assert.Equal(
            new[] { "fecha", "completo", "temperaturaMax", "temperaturaMin", "codigoCondicion", "descripcion", "probabilidadLluvia", "bloques" },
            dia.EnumerateObject().Select(p => p.Name));
        Assert.Equal("2026-10-27", dia.GetProperty("fecha").GetString());
        var bloque = dia.GetProperty("bloques")[0];
        Assert.Equal(
            new[] { "fechaHoraUtc", "temperatura", "humedad", "velocidadViento", "codigoCondicion", "descripcion", "probabilidadLluvia", "esDeDia" },
            bloque.EnumerateObject().Select(p => p.Name));
        Assert.Equal("2026-10-27T21:00:00Z", bloque.GetProperty("fechaHoraUtc").GetString());
    }
}
