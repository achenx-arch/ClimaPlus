using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Datos;

/// <summary>
/// E-02/E-03 a nivel de aplicación (EF InMemory): las consultas anónimas se pueden guardar y
/// nunca aparecen en datos personales. Las restricciones de PostgreSQL se prueban en
/// ConsultaClimaPostgreSqlTests, porque InMemory no aplica CHECK ni FK.
/// </summary>
public sealed class ConsultaAnonimaTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public ConsultaAnonimaTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task ConsultaAnonima_SeGuardaConIdUsuarioNull_YConservaSuDetalle()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        const string detalle = "{\"version\":1,\"zonaHorariaSegundos\":-21600,\"dias\":[]}";

        var idActual = await AyudasDatos.CrearConsultaAsync(_fabrica, null, idUbicacion, DateTime.UtcNow);
        var idPronostico = await AyudasDatos.CrearConsultaAsync(_fabrica, null, idUbicacion, DateTime.UtcNow, TiposConsulta.Pronostico, detalle: detalle);

        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var actual = await db.ConsultasClima.AsNoTracking().SingleAsync(c => c.IdConsulta == idActual);
        var pronostico = await db.ConsultasClima.AsNoTracking().SingleAsync(c => c.IdConsulta == idPronostico);

        Assert.Null(actual.IdUsuario);
        Assert.Null(actual.Detalle);
        Assert.Null(pronostico.IdUsuario);
        Assert.Null(pronostico.Temperatura);
        Assert.Equal(detalle, pronostico.Detalle);
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(pronostico.Detalle!).RootElement.GetProperty("dias").ValueKind);
    }

    [Fact]
    public async Task ConsultaAnonima_NoApareceComoUltimaConsultaDeUnFavorito()
    {
        // RN-BD-13 / RN-BD-42: una consulta anónima no entra en ningún dato personal.
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUsuario = await AyudasDatos.IdUsuarioAsync(_fabrica, correo);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var propia = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, DateTime.UtcNow.AddHours(-1), temperatura: 18m);
        await AyudasDatos.CrearConsultaAsync(_fabrica, null, idUbicacion, DateTime.UtcNow, temperatura: 30m);

        var crear = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/favoritos", token, new { idUbicacion }));
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var favorito = await crear.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(propia, favorito.GetProperty("ultimaConsulta").GetProperty("idConsulta").GetInt64());
        Assert.Equal(18m, favorito.GetProperty("ultimaConsulta").GetProperty("temperatura").GetDecimal());
    }

    [Fact]
    public async Task SoloConsultasAnonimas_UltimaConsultaEsNull()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        await AyudasDatos.CrearConsultaAsync(_fabrica, null, idUbicacion, DateTime.UtcNow);

        var crear = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/favoritos", token, new { idUbicacion }));
        var favorito = await crear.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Null, favorito.GetProperty("ultimaConsulta").ValueKind);
    }
}
