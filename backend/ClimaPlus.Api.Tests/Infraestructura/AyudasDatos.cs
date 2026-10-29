using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>Datos de prueba creados directamente en la base de datos InMemory.</summary>
public static class AyudasDatos
{
    private static int _secuencia;

    /// <summary>Crea una ubicación con coordenadas únicas y devuelve su id.</summary>
    public static async Task<long> CrearUbicacionAsync(ClimaPlusApiFactory fabrica, string nombre = "Cobán")
    {
        var n = Interlocked.Increment(ref _secuencia);
        using var alcance = fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var ubicacion = new Ubicacion
        {
            Nombre = nombre,
            Region = "Alta Verapaz",
            Pais = "GT",
            Latitud = 10m + (n * 0.0001m),
            Longitud = -80m - (n * 0.0001m),
            FechaCreacion = DateTime.UtcNow,
        };
        db.Ubicaciones.Add(ubicacion);
        await db.SaveChangesAsync();
        return ubicacion.IdUbicacion;
    }

    /// <summary>Registra una consulta solo en la base InMemory de pruebas. idUsuario null = visitante (E-02).</summary>
    public static async Task<long> CrearConsultaAsync(
        ClimaPlusApiFactory fabrica, long? idUsuario, long idUbicacion, DateTime fecha, string tipo = TiposConsulta.Actual, decimal temperatura = 20m, string? detalle = null)
    {
        using var alcance = fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var actual = tipo == TiposConsulta.Actual;
        var consulta = new ConsultaClima
        {
            IdUsuario = idUsuario,
            IdUbicacion = idUbicacion,
            TipoConsulta = tipo,
            Temperatura = actual ? temperatura : null,
            SensacionTermica = actual ? temperatura : null,
            Humedad = actual ? (short)70 : null,
            VelocidadViento = actual ? 3m : null,
            CodigoCondicion = actual ? (short)500 : null,
            DescripcionCondicion = actual ? "lluvia ligera" : null,
            // E-03: PRONOSTICO siempre lleva detalle (RN-BD-43).
            Detalle = detalle ?? (actual ? null : "{\"version\":1,\"zonaHorariaSegundos\":-21600,\"dias\":[]}"),
            FechaConsulta = fecha,
        };
        db.ConsultasClima.Add(consulta);
        await db.SaveChangesAsync();
        return consulta.IdConsulta;
    }

    /// <summary>
    /// Crea una alerta SOLO en la base de datos InMemory de las pruebas (el motor real llega con E-02/E-03).
    /// Origen OpenWeather: crea una consulta ACTUAL. Origen ESP32 (<paramref name="desdeEsp32"/>): crea dispositivo y lectura.
    /// </summary>
    public static async Task<long> CrearAlertaAsync(
        ClimaPlusApiFactory fabrica,
        long idUsuario,
        long idUbicacion,
        string tipo = TiposAlerta.TemperaturaAlta,
        string estado = EstadosAlerta.Nueva,
        DateTime? fecha = null,
        bool desdeEsp32 = false,
        string? nombreDispositivo = null)
    {
        var generada = fecha ?? DateTime.UtcNow;
        long? idConsulta = null;
        long? idLectura = null;

        using var alcance = fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();

        if (desdeEsp32)
        {
            var dispositivo = new Dispositivo
            {
                IdUsuario = idUsuario,
                IdUbicacion = idUbicacion,
                Nombre = nombreDispositivo ?? "ESP32 " + Guid.NewGuid().ToString("N")[..6],
                ApiKeyHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                Activo = true,
                FechaCreacion = generada,
            };
            db.Dispositivos.Add(dispositivo);
            await db.SaveChangesAsync();

            var lectura = new LecturaSensor
            {
                IdDispositivo = dispositivo.IdDispositivo,
                Temperatura = 33.4m,
                Lluvia = tipo == TiposAlerta.Lluvia,
                FechaLectura = generada,
            };
            db.LecturasSensor.Add(lectura);
            await db.SaveChangesAsync();
            idLectura = lectura.IdLectura;
        }
        else
        {
            idConsulta = await CrearConsultaAsync(fabrica, idUsuario, idUbicacion, generada, TiposConsulta.Actual, 33.4m);
        }

        var lluvia = tipo == TiposAlerta.Lluvia;
        var alerta = new Alerta
        {
            IdUsuario = idUsuario,
            IdUbicacion = idUbicacion,
            IdConsulta = idConsulta,
            IdLectura = idLectura,
            Tipo = tipo,
            ValorDetectado = lluvia ? null : 33.4m,
            ValorUmbral = lluvia ? null : 32m,
            Mensaje = lluvia ? "Lluvia detectada" : "33.4 °C supera tu umbral de 32 °C",
            Estado = estado,
            FechaGeneracion = generada,
        };
        db.Alertas.Add(alerta);
        await db.SaveChangesAsync();
        return alerta.IdAlerta;
    }

    public static async Task<long> IdUsuarioAsync(ClimaPlusApiFactory fabrica, string correo) =>
        (await Ayudas.LeerUsuarioAsync(fabrica, correo)).IdUsuario;
}
