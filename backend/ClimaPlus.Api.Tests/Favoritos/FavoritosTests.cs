using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Favoritos;

/// <summary>Fase 2F: /api/favoritos (JWT obligatorio, aislamiento por usuario, RN-BD-02).</summary>
public sealed class FavoritosTests : IClassFixture<ClimaPlusApiFactory>
{
    private const string Ruta = "/api/favoritos";

    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public FavoritosTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, string token, object? cuerpo = null) =>
        _cliente.SendAsync(Ayudas.ConToken(metodo, ruta, token, cuerpo));

    private Task<HttpResponseMessage> AgregarAsync(string token, long idUbicacion) =>
        EnviarAsync(HttpMethod.Post, Ruta, token, new { idUbicacion });

    private async Task<JsonElement[]> ListarAsync(string token)
    {
        var respuesta = await EnviarAsync(HttpMethod.Get, Ruta, token);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    private static async Task<long> IdFavoritoAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("idFavorito").GetInt64();
    }

    // ------------------------------------------------------------------ Autenticación

    [Theory]
    [InlineData("GET", Ruta)]
    [InlineData("GET", Ruta + "/1")]
    [InlineData("POST", Ruta)]
    [InlineData("DELETE", Ruta + "/1")]
    public async Task SinToken_Responde401(string metodo, string ruta)
    {
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo == "POST")
        {
            solicitud.Content = JsonContent.Create(new { idUbicacion = 1 });
        }

        var respuesta = await _cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    [Fact]
    public async Task TokenInvalido_Responde401()
    {
        var respuesta = await EnviarAsync(HttpMethod.Get, Ruta, "token-invalido");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task UsuarioDesactivado_Responde401()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);

        var respuesta = await EnviarAsync(HttpMethod.Get, Ruta, token);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ Agregar y listar

    [Fact]
    public async Task Agregar_Responde201ConLocationYLaUbicacion()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Cobán");

        var respuesta = await AgregarAsync(token, idUbicacion);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var idFavorito = cuerpo.GetProperty("idFavorito").GetInt64();
        Assert.True(idFavorito > 0);
        Assert.Equal($"/api/favoritos/{idFavorito}", respuesta.Headers.Location?.AbsolutePath);
        Assert.Equal(idUbicacion, cuerpo.GetProperty("ubicacion").GetProperty("idUbicacion").GetInt64());
        Assert.Equal("Cobán", cuerpo.GetProperty("ubicacion").GetProperty("nombre").GetString());
        Assert.Equal("GT", cuerpo.GetProperty("ubicacion").GetProperty("pais").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("ultimaConsulta").ValueKind);
        Assert.EndsWith("Z", cuerpo.GetProperty("fechaCreacion").GetString());   // UTC

        var obtenido = await EnviarAsync(HttpMethod.Get, $"{Ruta}/{idFavorito}", token);
        Assert.Equal(HttpStatusCode.OK, obtenido.StatusCode);
    }

    [Fact]
    public async Task Listar_SinFavoritos_RespondeListaVacia()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        Assert.Empty(await ListarAsync(token));
    }

    [Fact]
    public async Task Listar_DevuelveSoloLosPropiosDelMasRecienteAlMasAntiguo()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (_, otroToken) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var primera = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Primera");
        var segunda = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Segunda");
        var ajena = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Ajena");

        await IdFavoritoAsync(await AgregarAsync(token, primera));
        await Task.Delay(20);
        await IdFavoritoAsync(await AgregarAsync(token, segunda));
        await IdFavoritoAsync(await AgregarAsync(otroToken, ajena));

        var favoritos = await ListarAsync(token);

        Assert.Equal(
            new[] { "Segunda", "Primera" },
            favoritos.Select(f => f.GetProperty("ubicacion").GetProperty("nombre").GetString()));
    }

    [Fact]
    public async Task Agregar_MismaUbicacionDosVeces_Responde409()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        await IdFavoritoAsync(await AgregarAsync(token, idUbicacion));

        var repetido = await AgregarAsync(token, idUbicacion);

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(repetido);
        Assert.True(problema.GetProperty("errors").TryGetProperty("idUbicacion", out _));
        Assert.Single(await ListarAsync(token));
    }

    [Fact]
    public async Task Agregar_MismaUbicacionPorDosUsuarios_EsValido()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (_, otroToken) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);

        Assert.Equal(HttpStatusCode.Created, (await AgregarAsync(token, idUbicacion)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AgregarAsync(otroToken, idUbicacion)).StatusCode);
    }

    [Fact]
    public async Task Agregar_UbicacionInexistente_Responde404()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await AgregarAsync(token, 987_654_321);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Empty(await ListarAsync(token));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"idUbicacion\":null}")]
    [InlineData("{\"idUbicacion\":0}")]
    [InlineData("{\"idUbicacion\":-5}")]
    [InlineData("{\"idUbicacion\":\"abc\"}")]
    [InlineData("{\"idUbicacion\":1.5}")]
    [InlineData("{\"idUbicacion\":1,\"idUsuario\":2}")]
    [InlineData("{\"idUbicacion\":1,\"nombre\":\"<script>alert(1)</script>\"}")]
    [InlineData("no es json")]
    public async Task Agregar_CuerpoInvalido_Responde400(string cuerpo)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var solicitud = Ayudas.ConToken(HttpMethod.Post, Ruta, token);
        solicitud.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");

        var respuesta = await _cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Empty(await ListarAsync(token));
    }

    [Fact]
    public async Task Administrador_TambienTieneFavoritos()
    {
        var correo = await Ayudas.CrearAdministradorAsync(_fabrica);
        var token = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);

        Assert.Equal(HttpStatusCode.Created, (await AgregarAsync(token, idUbicacion)).StatusCode);
        Assert.Single(await ListarAsync(token));
    }

    // ------------------------------------------------------------------ Aislamiento

    [Fact]
    public async Task OtroUsuario_NoPuedeVerNiEliminarElFavorito()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (_, intruso) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idFavorito = await IdFavoritoAsync(await AgregarAsync(token, await AyudasDatos.CrearUbicacionAsync(_fabrica)));

        var ver = await EnviarAsync(HttpMethod.Get, $"{Ruta}/{idFavorito}", intruso);
        var eliminar = await EnviarAsync(HttpMethod.Delete, $"{Ruta}/{idFavorito}", intruso);

        // 404 (no 403): no se revela que el favorito existe.
        Assert.Equal(HttpStatusCode.NotFound, ver.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, eliminar.StatusCode);
        Assert.Empty(await ListarAsync(intruso));
        Assert.Single(await ListarAsync(token));
    }

    // ------------------------------------------------------------------ Eliminar

    [Fact]
    public async Task Eliminar_Responde204YConservaLaUbicacion()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var idFavorito = await IdFavoritoAsync(await AgregarAsync(token, idUbicacion));

        var eliminar = await EnviarAsync(HttpMethod.Delete, $"{Ruta}/{idFavorito}", token);
        var otraVez = await EnviarAsync(HttpMethod.Delete, $"{Ruta}/{idFavorito}", token);
        var ver = await EnviarAsync(HttpMethod.Get, $"{Ruta}/{idFavorito}", token);

        Assert.Equal(HttpStatusCode.NoContent, eliminar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otraVez.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ver.StatusCode);
        Assert.Empty(await ListarAsync(token));

        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        Assert.True(await db.Ubicaciones.AnyAsync(u => u.IdUbicacion == idUbicacion));

        // Después de eliminarla se puede volver a agregar.
        Assert.Equal(HttpStatusCode.Created, (await AgregarAsync(token, idUbicacion)).StatusCode);
    }

    [Theory]
    [InlineData("/api/favoritos/abc")]
    [InlineData("/api/favoritos/1.5")]
    [InlineData("/api/favoritos/99999999999999999999")]
    public async Task IdNoNumerico_Responde404(string ruta)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await EnviarAsync(HttpMethod.Delete, ruta, token);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ Última consulta (M06/M07)

    [Fact]
    public async Task UltimaConsulta_EsLaActualMasRecienteDelPropioUsuario()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (otroCorreo, _) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUsuario = await AyudasDatos.IdUsuarioAsync(_fabrica, correo);
        var idOtro = await AyudasDatos.IdUsuarioAsync(_fabrica, otroCorreo);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var hace = DateTime.UtcNow;

        await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, hace.AddHours(-3), temperatura: 18m);
        var esperada = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, hace.AddHours(-2), temperatura: 21.5m);
        await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, hace.AddHours(-1), TiposConsulta.Pronostico);
        await AyudasDatos.CrearConsultaAsync(_fabrica, idOtro, idUbicacion, hace, temperatura: 30m);

        await IdFavoritoAsync(await AgregarAsync(token, idUbicacion));
        var favorito = Assert.Single(await ListarAsync(token));

        var ultima = favorito.GetProperty("ultimaConsulta");
        Assert.Equal(esperada, ultima.GetProperty("idConsulta").GetInt64());
        Assert.Equal(21.5m, ultima.GetProperty("temperatura").GetDecimal());
        Assert.Equal(500, ultima.GetProperty("codigoCondicion").GetInt32());
        Assert.Equal("lluvia ligera", ultima.GetProperty("descripcion").GetString());
    }
}
