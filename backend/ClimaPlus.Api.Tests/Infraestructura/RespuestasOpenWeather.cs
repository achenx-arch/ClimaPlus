using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>JSON con la forma de las respuestas reales de OpenWeather (plan gratuito), para el proveedor simulado.</summary>
public static class RespuestasOpenWeather
{
    public sealed record Lugar(string? Nombre, decimal? Latitud, decimal? Longitud, string? Pais, string? Estado = null, string? NombreEs = null);

    public static string Geocoding(params Lugar[] lugares) =>
        JsonSerializer.Serialize(lugares.Select(l => new Dictionary<string, object?>
        {
            ["name"] = l.Nombre,
            ["local_names"] = l.NombreEs is null ? null : new Dictionary<string, string> { ["es"] = l.NombreEs, ["en"] = l.Nombre ?? string.Empty },
            ["lat"] = l.Latitud,
            ["lon"] = l.Longitud,
            ["country"] = l.Pais,
            ["state"] = l.Estado,
        }));

    public static readonly Lugar Coban = new("Coban", 15.47083m, -90.37083m, "GT", "Alta Verapaz", "Cobán");

    public static readonly Lugar Guatemala = new("Guatemala City", 14.6417889m, -90.5132239m, "GT", "Guatemala", "Ciudad de Guatemala");

    /// <summary>GET /data/2.5/weather válido (Cobán de día).</summary>
    public static JsonObject ClimaActual() => new()
    {
        ["coord"] = new JsonObject { ["lon"] = -90.3708m, ["lat"] = 15.4708m },
        ["weather"] = new JsonArray(new JsonObject { ["id"] = 802, ["main"] = "Clouds", ["description"] = "nubes dispersas", ["icon"] = "03d" }),
        ["main"] = new JsonObject
        {
            ["temp"] = 22.456m,
            ["feels_like"] = 22.1m,
            ["temp_min"] = 16m,
            ["temp_max"] = 25m,
            ["pressure"] = 1018,
            ["humidity"] = 70,
        },
        ["wind"] = new JsonObject { ["speed"] = 3.1m, ["deg"] = 45 },
        ["dt"] = 1_793_125_200L,                 // 2026-10-27T18:20:00Z
        ["sys"] = new JsonObject { ["country"] = "GT", ["sunrise"] = 1_793_102_280L, ["sunset"] = 1_793_144_460L },
        ["timezone"] = -21600,
        ["name"] = "Cobán",
        ["cod"] = 200,
    };

    /// <summary>Pronóstico de 40 bloques que empieza en el siguiente múltiplo de 3 h después de <paramref name="ahoraUtc"/>.</summary>
    public static JsonObject PronosticoDesde(DateTime ahoraUtc, int zonaHorariaSegundos = -21600)
    {
        var inicio = new DateTime(ahoraUtc.Year, ahoraUtc.Month, ahoraUtc.Day, ahoraUtc.Hour / 3 * 3, 0, 0, DateTimeKind.Utc).AddHours(3);
        return Pronostico(inicio, 40, zonaHorariaSegundos);
    }

    /// <summary>
    /// GET /data/2.5/forecast con <paramref name="bloques"/> bloques cada 3 h desde <paramref name="inicioUtc"/>.
    /// Bloque i: temp = 15 + i % 8, pop = (i % 5) / 10, condición 800 + i % 4.
    /// </summary>
    public static JsonObject Pronostico(DateTime inicioUtc, int bloques = 40, int zonaHorariaSegundos = -21600)
    {
        var lista = new JsonArray();
        for (var i = 0; i < bloques; i++)
        {
            var fecha = inicioUtc.AddHours(3 * i);
            var horaLocal = fecha.AddSeconds(zonaHorariaSegundos).Hour;
            var dia = horaLocal is >= 6 and < 18;
            var codigo = 800 + (i % 4);
            lista.Add(new JsonObject
            {
                ["dt"] = new DateTimeOffset(fecha, TimeSpan.Zero).ToUnixTimeSeconds(),
                ["main"] = new JsonObject { ["temp"] = 15m + (i % 8), ["feels_like"] = 15m, ["humidity"] = 60 + (i % 10) },
                ["weather"] = new JsonArray(new JsonObject { ["id"] = codigo, ["description"] = $"condicion {codigo}", ["icon"] = dia ? "02d" : "02n" }),
                ["wind"] = new JsonObject { ["speed"] = 2.5m, ["deg"] = 90 },
                ["pop"] = (i % 5) / 10m,
                ["sys"] = new JsonObject { ["pod"] = dia ? "d" : "n" },
                ["dt_txt"] = fecha.ToString("yyyy-MM-dd HH:mm:ss"),
            });
        }

        return new JsonObject
        {
            ["cod"] = "200",
            ["cnt"] = bloques,
            ["list"] = lista,
            ["city"] = new JsonObject { ["name"] = "Cobán", ["country"] = "GT", ["timezone"] = zonaHorariaSegundos },
        };
    }
}
