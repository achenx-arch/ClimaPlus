using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Services;
using ClimaPlus.Api.Services.Admin;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Admin;

/// <summary>
/// Gestión de usuarios del administrador (M14): RF-26 listar/filtrar, RF-27 cambiar rol, RF-28 activar/desactivar.
/// Base InMemory (sin PostgreSQL). La concurrencia real se prueba en Datos/AdminUsuariosPostgreSqlTests.
/// </summary>
public sealed class AdminUsuariosTests : IClassFixture<ClimaPlusApiFactory>
{
    private const string Ruta = "/api/admin/usuarios";

    private static readonly string[] CamposPermitidos =
    {
        "idUsuario", "nombre", "correo", "rol", "activo", "correoVerificado", "tienePassword", "tieneGoogle", "fechaCreacion",
    };

    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public AdminUsuariosTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    // ------------------------------------------------------------------ Apoyo

    private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, string? token, object? cuerpo = null)
    {
        if (token is null)
        {
            var anonima = new HttpRequestMessage(metodo, ruta);
            if (cuerpo is not null)
            {
                anonima.Content = JsonContent.Create(cuerpo);
            }

            return _cliente.SendAsync(anonima);
        }

        return _cliente.SendAsync(Ayudas.ConToken(metodo, ruta, token, cuerpo));
    }

    private Task<HttpResponseMessage> EnviarJsonAsync(HttpMethod metodo, string ruta, string token, string json)
    {
        var solicitud = Ayudas.ConToken(metodo, ruta, token);
        solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return _cliente.SendAsync(solicitud);
    }

    private async Task<JsonElement> OkAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> GetOkAsync(string ruta, string token) => await OkAsync(await EnviarAsync(HttpMethod.Get, ruta, token));

    private Task<HttpResponseMessage> CambiarRolAsync(string token, long idUsuario, string rol) =>
        EnviarAsync(HttpMethod.Patch, $"{Ruta}/{idUsuario}/rol", token, new { rol });

    private Task<HttpResponseMessage> CambiarEstadoAsync(string token, long idUsuario, bool activo) =>
        EnviarAsync(HttpMethod.Patch, $"{Ruta}/{idUsuario}/estado", token, new { activo });

    /// <summary>Administrador activo creado en la base, con sesión iniciada.</summary>
    private async Task<(string Correo, string Token, long Id)> AdministradorAsync()
    {
        var correo = await Ayudas.CrearAdministradorAsync(_fabrica);
        var token = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        return (correo, token, await AyudasDatos.IdUsuarioAsync(_fabrica, correo));
    }

    /// <summary>Usuario normal registrado por la API, con sesión iniciada.</summary>
    private async Task<(string Correo, string Token, long Id)> UsuarioAsync()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        return (correo, token, await AyudasDatos.IdUsuarioAsync(_fabrica, correo));
    }

    /// <summary>Inserta usuarios directamente (sin sesión), con un marcador común en el nombre para aislarlos en la búsqueda.</summary>
    private async Task<List<Usuario>> InsertarAsync(string marcador, int cantidad, Func<int, Usuario, Usuario>? ajuste = null)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var inicio = DateTime.UtcNow.AddDays(-1);
        var usuarios = new List<Usuario>();
        for (var i = 0; i < cantidad; i++)
        {
            var usuario = new Usuario
            {
                Nombre = $"{marcador} Persona {i:00}",
                Correo = $"{marcador.ToLowerInvariant()}-{i:00}-{Guid.NewGuid():N}@ejemplo.com",
                PasswordHash = "no-es-un-hash-real",
                Rol = Roles.Usuario,
                Activo = true,
                CorreoVerificado = true,
                FechaCreacion = inicio.AddMinutes(i),
            };
            usuarios.Add(ajuste is null ? usuario : ajuste(i, usuario));
        }

        db.Usuarios.AddRange(usuarios);
        await db.SaveChangesAsync();
        return usuarios;
    }

    private static string Marcador() => "Lote" + Guid.NewGuid().ToString("N")[..10];

    private static long[] Ids(JsonElement pagina) =>
        pagina.GetProperty("elementos").EnumerateArray().Select(u => u.GetProperty("idUsuario").GetInt64()).ToArray();

    private static void SinDatosSensibles(string cuerpo)
    {
        foreach (var prohibido in new[] { "\"password\"", "passwordHash", "hash", "sello", "token", "codigo", "desafio", "sujeto", "no-es-un-hash-real", "AQAAAA" })
        {
            Assert.DoesNotContain(prohibido, cuerpo, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------ Autorización

    [Theory]
    [InlineData("GET", Ruta)]
    [InlineData("GET", Ruta + "/1")]
    [InlineData("PATCH", Ruta + "/1/rol")]
    [InlineData("PATCH", Ruta + "/1/estado")]
    public async Task SinSesion_Responde401(string metodo, string ruta)
    {
        var respuesta = await EnviarAsync(new HttpMethod(metodo), ruta, null, metodo == "PATCH" ? new { rol = Roles.Administrador } : null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task UsuarioNormal_Responde403_YNoCambiaNada()
    {
        var (correo, token, id) = await UsuarioAsync();

        var lista = await EnviarAsync(HttpMethod.Get, Ruta, token);
        var detalle = await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", token);
        var rol = await CambiarRolAsync(token, id, Roles.Administrador);       // intento de autopromoción
        var estado = await CambiarEstadoAsync(token, id, false);

        Assert.Equal(HttpStatusCode.Forbidden, lista.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, detalle.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rol.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, estado.StatusCode);
        var guardado = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(Roles.Usuario, guardado.Rol);
        Assert.True(guardado.Activo);
    }

    [Fact]
    public async Task RegistroPublico_NoAceptaRol()
    {
        var correo = Ayudas.CorreoUnico();

        var respuesta = await _cliente.PostAsJsonAsync("/api/auth/register",
            new { nombre = "Intento", correo, password = Ayudas.PasswordValida, rol = Roles.Administrador });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ RF-26

    [Fact]
    public async Task Listar_Pagina20_MasRecientesPrimero_SinDatosSensibles()
    {
        var (_, admin, _) = await AdministradorAsync();
        var marcador = Marcador();
        var usuarios = await InsertarAsync(marcador, 25);
        var esperados = usuarios.OrderByDescending(u => u.FechaCreacion).Select(u => u.IdUsuario).ToArray();

        var respuesta1 = await EnviarAsync(HttpMethod.Get, $"{Ruta}?busqueda={marcador}", admin);
        var cuerpo1 = await respuesta1.Content.ReadAsStringAsync();
        var pagina1 = await OkAsync(respuesta1);
        var pagina2 = await GetOkAsync($"{Ruta}?busqueda={marcador}&pagina=2", admin);
        var pagina3 = await GetOkAsync($"{Ruta}?busqueda={marcador}&pagina=3", admin);

        Assert.Equal(25, pagina1.GetProperty("totalElementos").GetInt32());
        Assert.Equal(2, pagina1.GetProperty("totalPaginas").GetInt32());
        Assert.Equal(20, pagina1.GetProperty("tamanoPagina").GetInt32());
        Assert.Equal(esperados[..20], Ids(pagina1));
        Assert.Equal(esperados[20..], Ids(pagina2));
        Assert.Empty(Ids(pagina3));
        foreach (var elemento in pagina1.GetProperty("elementos").EnumerateArray())
        {
            Assert.Equal(CamposPermitidos.OrderBy(c => c), elemento.EnumerateObject().Select(p => p.Name).OrderBy(c => c));
        }

        SinDatosSensibles(cuerpo1);
    }

    [Fact]
    public async Task Listar_BuscaPorNombreOCorreo_SinDistinguirMayusculas()
    {
        var (_, admin, _) = await AdministradorAsync();
        var marcador = Marcador();
        var usuarios = await InsertarAsync(marcador, 3);

        var porNombre = await GetOkAsync($"{Ruta}?busqueda={Uri.EscapeDataString(marcador.ToUpperInvariant() + " persona 01")}", admin);
        var porCorreo = await GetOkAsync($"{Ruta}?busqueda={Uri.EscapeDataString(usuarios[2].Correo.ToUpperInvariant())}", admin);
        var conEspacios = await GetOkAsync($"{Ruta}?busqueda={Uri.EscapeDataString("  " + marcador + "  ")}", admin);
        var nada = await GetOkAsync($"{Ruta}?busqueda=no-existe-{Guid.NewGuid():N}", admin);

        Assert.Equal(new[] { usuarios[1].IdUsuario }, Ids(porNombre));
        Assert.Equal(new[] { usuarios[2].IdUsuario }, Ids(porCorreo));
        Assert.Equal(3, conEspacios.GetProperty("totalElementos").GetInt32());
        Assert.Equal(0, nada.GetProperty("totalElementos").GetInt32());
    }

    [Fact]
    public async Task Listar_FiltraPorRolYEstado()
    {
        var (_, admin, _) = await AdministradorAsync();
        var marcador = Marcador();
        // 0: USUARIO activo · 1: USUARIO inactivo · 2: ADMINISTRADOR activo · 3: ADMINISTRADOR inactivo
        var u = await InsertarAsync(marcador, 4, (i, usuario) =>
        {
            usuario.Rol = i >= 2 ? Roles.Administrador : Roles.Usuario;
            usuario.Activo = i % 2 == 0;
            return usuario;
        });
        var b = $"{Ruta}?busqueda={marcador}";

        Assert.Equal(new[] { u[1].IdUsuario, u[0].IdUsuario }, Ids(await GetOkAsync($"{b}&rol=USUARIO", admin)));
        Assert.Equal(new[] { u[3].IdUsuario, u[2].IdUsuario }, Ids(await GetOkAsync($"{b}&rol=ADMINISTRADOR", admin)));
        Assert.Equal(new[] { u[2].IdUsuario, u[0].IdUsuario }, Ids(await GetOkAsync($"{b}&estado=ACTIVO", admin)));
        Assert.Equal(new[] { u[3].IdUsuario, u[1].IdUsuario }, Ids(await GetOkAsync($"{b}&estado=INACTIVO", admin)));
        Assert.Equal(new[] { u[3].IdUsuario }, Ids(await GetOkAsync($"{b}&rol=ADMINISTRADOR&estado=INACTIVO", admin)));
    }

    [Theory]
    [InlineData("?rol=ROOT", "rol")]
    [InlineData("?rol=administrador", "rol")]
    [InlineData("?estado=BORRADO", "estado")]
    [InlineData("?pagina=0", "pagina")]
    [InlineData("?pagina=abc", "pagina")]
    public async Task Listar_FiltroInvalido_Responde400ConElCampo(string consulta, string campo)
    {
        var (_, admin, _) = await AdministradorAsync();

        var respuesta = await EnviarAsync(HttpMethod.Get, Ruta + consulta, admin);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Contains(problema.GetProperty("errors").EnumerateObject(), e => e.Name.Equals(campo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Listar_BusquedaDemasiadoLarga_Responde400()
    {
        var (_, admin, _) = await AdministradorAsync();

        var respuesta = await EnviarAsync(HttpMethod.Get, $"{Ruta}?busqueda={new string('a', 151)}", admin);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Detalle_DevuelveElUsuarioO404()
    {
        var (_, admin, _) = await AdministradorAsync();
        var (correo, _, id) = await UsuarioAsync();

        var respuesta = await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", admin);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        var detalle = await OkAsync(respuesta);
        var inexistente = await EnviarAsync(HttpMethod.Get, $"{Ruta}/987654321", admin);

        Assert.Equal(correo, detalle.GetProperty("correo").GetString());
        Assert.Equal(Roles.Usuario, detalle.GetProperty("rol").GetString());
        Assert.True(detalle.GetProperty("activo").GetBoolean());
        Assert.True(detalle.GetProperty("tienePassword").GetBoolean());
        Assert.False(detalle.GetProperty("tieneGoogle").GetBoolean());
        SinDatosSensibles(cuerpo);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    // ------------------------------------------------------------------ RF-27

    [Fact]
    public async Task CambiarRol_PromueveYDegrada_RenuevaElSello_YCierraSesiones()
    {
        var (_, admin, idAdmin) = await AdministradorAsync();
        var (correo, tokenUsuario, id) = await UsuarioAsync();
        var selloInicial = (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).SelloSeguridad;

        var promovido = await OkAsync(await CambiarRolAsync(admin, id, Roles.Administrador));

        Assert.Equal(Roles.Administrador, promovido.GetProperty("rol").GetString());
        var tras = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(Roles.Administrador, tras.Rol);
        Assert.NotEqual(selloInicial, tras.SelloSeguridad);
        // La sesión que tenía como USUARIO deja de valer.
        Assert.Equal(HttpStatusCode.Unauthorized, (await EnviarAsync(HttpMethod.Get, "/api/usuarios/me", tokenUsuario)).StatusCode);
        // Con una sesión nueva ya entra a la administración.
        var tokenNuevo = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(HttpMethod.Get, Ruta, tokenNuevo)).StatusCode);

        var degradado = await OkAsync(await CambiarRolAsync(admin, id, Roles.Usuario));

        Assert.Equal(Roles.Usuario, degradado.GetProperty("rol").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await EnviarAsync(HttpMethod.Get, Ruta, tokenNuevo)).StatusCode);
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains($"admin_rol_cambiado idAdmin={idAdmin} idUsuario={id} anterior=USUARIO nuevo=ADMINISTRADOR", StringComparison.Ordinal));
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains($"admin_rol_cambiado idAdmin={idAdmin} idUsuario={id} anterior=ADMINISTRADOR nuevo=USUARIO", StringComparison.Ordinal));
        Assert.DoesNotContain(_fabrica.Logs.Mensajes, m => m.Contains("admin_", StringComparison.Ordinal) && m.Contains(correo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CambiarRol_MismoRol_NoRenuevaElSello()
    {
        var (_, admin, _) = await AdministradorAsync();
        var (correo, tokenUsuario, id) = await UsuarioAsync();
        var selloInicial = (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).SelloSeguridad;

        var respuesta = await OkAsync(await CambiarRolAsync(admin, id, Roles.Usuario));

        Assert.Equal(Roles.Usuario, respuesta.GetProperty("rol").GetString());
        Assert.Equal(selloInicial, (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).SelloSeguridad);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(HttpMethod.Get, "/api/usuarios/me", tokenUsuario)).StatusCode);
    }

    [Fact]
    public async Task CambiarRol_PropioRol_Responde409_YSigueSiendoAdministrador()
    {
        var (correo, admin, idAdmin) = await AdministradorAsync();

        var respuesta = await CambiarRolAsync(admin, idAdmin, Roles.Usuario);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty("rol", out _));
        Assert.Equal(Roles.Administrador, (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).Rol);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(HttpMethod.Get, Ruta, admin)).StatusCode);   // su sesión sigue
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains($"admin_rol_rechazado motivo=propio idAdmin={idAdmin}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CambiarRol_DosAdministradores_ElDegradadoPierdeLaSesion()
    {
        var (_, adminA, _) = await AdministradorAsync();
        var (correoB, adminB, idB) = await AdministradorAsync();

        await OkAsync(await CambiarRolAsync(adminA, idB, Roles.Usuario));

        Assert.Equal(Roles.Usuario, (await Ayudas.LeerUsuarioAsync(_fabrica, correoB)).Rol);
        Assert.Equal(HttpStatusCode.Unauthorized, (await EnviarAsync(HttpMethod.Get, Ruta, adminB)).StatusCode);
    }

    [Theory]
    [InlineData("{\"rol\":\"ROOT\"}")]
    [InlineData("{\"rol\":\"administrador\"}")]
    [InlineData("{\"rol\":\"\"}")]
    [InlineData("{}")]
    [InlineData("{\"rol\":\"ADMINISTRADOR\",\"activo\":false}")]
    [InlineData("{\"rol\":\"ADMINISTRADOR\",\"passwordHash\":\"x\"}")]
    public async Task CambiarRol_CuerpoInvalido_Responde400_SinCambios(string json)
    {
        var (_, admin, _) = await AdministradorAsync();
        var (correo, _, id) = await UsuarioAsync();

        var respuesta = await EnviarJsonAsync(HttpMethod.Patch, $"{Ruta}/{id}/rol", admin, json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var usuario = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(Roles.Usuario, usuario.Rol);
        Assert.True(usuario.Activo);
    }

    [Fact]
    public async Task CambiarRol_UsuarioInexistente_Responde404()
    {
        var (_, admin, _) = await AdministradorAsync();

        var respuesta = await CambiarRolAsync(admin, 987654321, Roles.Administrador);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ RF-28

    [Fact]
    public async Task CambiarEstado_DesactivaYReactiva_RenuevaElSello_YCierraSesiones()
    {
        var (_, admin, idAdmin) = await AdministradorAsync();
        var (correo, tokenUsuario, id) = await UsuarioAsync();
        var selloInicial = (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).SelloSeguridad;

        var desactivado = await OkAsync(await CambiarEstadoAsync(admin, id, false));

        Assert.False(desactivado.GetProperty("activo").GetBoolean());
        var tras = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.False(tras.Activo);
        Assert.NotEqual(selloInicial, tras.SelloSeguridad);
        Assert.Equal(HttpStatusCode.Unauthorized, (await EnviarAsync(HttpMethod.Get, "/api/usuarios/me", tokenUsuario)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);

        var reactivado = await OkAsync(await CambiarEstadoAsync(admin, id, true));

        Assert.True(reactivado.GetProperty("activo").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await EnviarAsync(HttpMethod.Get, "/api/usuarios/me", tokenUsuario)).StatusCode);   // la sesión anterior no revive
        var tokenNuevo = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(HttpMethod.Get, "/api/usuarios/me", tokenNuevo)).StatusCode);
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains($"admin_estado_cambiado idAdmin={idAdmin} idUsuario={id} activo=False", StringComparison.Ordinal));
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains($"admin_estado_cambiado idAdmin={idAdmin} idUsuario={id} activo=True", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CambiarEstado_MismoEstado_NoRenuevaElSello()
    {
        var (_, admin, _) = await AdministradorAsync();
        var (correo, _, id) = await UsuarioAsync();
        var selloInicial = (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).SelloSeguridad;

        await OkAsync(await CambiarEstadoAsync(admin, id, true));

        Assert.Equal(selloInicial, (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).SelloSeguridad);
    }

    [Fact]
    public async Task CambiarEstado_PropiaCuenta_Responde409_YSigueActiva()
    {
        var (correo, admin, idAdmin) = await AdministradorAsync();

        var respuesta = await CambiarEstadoAsync(admin, idAdmin, false);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty("activo", out _));
        Assert.True((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).Activo);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(HttpMethod.Get, Ruta, admin)).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"activo\":null}")]
    [InlineData("{\"activo\":\"no\"}")]
    [InlineData("{\"activo\":false,\"rol\":\"USUARIO\"}")]
    public async Task CambiarEstado_CuerpoInvalido_Responde400_SinCambios(string json)
    {
        var (_, admin, _) = await AdministradorAsync();
        var (correo, _, id) = await UsuarioAsync();

        var respuesta = await EnviarJsonAsync(HttpMethod.Patch, $"{Ruta}/{id}/estado", admin, json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).Activo);
    }

    [Fact]
    public async Task CambiarEstado_UsuarioInexistente_Responde404()
    {
        var (_, admin, _) = await AdministradorAsync();

        var respuesta = await CambiarEstadoAsync(admin, 987654321, false);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ RN-BD-09 (último administrador activo)

    /// <summary>
    /// Por la API, un administrador activo siempre está autenticado y no puede tocarse a sí mismo, así que el "último
    /// administrador" solo se alcanza con dos peticiones simultáneas (probado con PostgreSQL). Aquí se comprueba la regla
    /// en el servicio, con una base propia que tiene un único administrador activo.
    /// </summary>
    [Fact]
    public async Task UltimoAdministradorActivo_NoSeDegradaNiSeDesactiva()
    {
        await using var fabrica = new ClimaPlusApiFactory();
        var correoUnico = await Ayudas.CrearAdministradorAsync(fabrica);
        var idUnico = await AyudasDatos.IdUsuarioAsync(fabrica, correoUnico);
        var selloInicial = (await Ayudas.LeerUsuarioAsync(fabrica, correoUnico)).SelloSeguridad;
        const long otroAdministrador = 987654321;   // el que lo intenta (ya degradado por una petición simultánea)

        using (var alcance = fabrica.Services.CreateScope())
        {
            var servicio = alcance.ServiceProvider.GetRequiredService<UsuarioAdminService>();

            var rol = await servicio.CambiarRolAsync(otroAdministrador, idUnico, Roles.Usuario, "127.0.0.1", default);
            var estado = await servicio.CambiarEstadoAsync(otroAdministrador, idUnico, false, "127.0.0.1", default);

            Assert.False(rol.Exitoso);
            Assert.Equal(TipoError.Conflicto, rol.Error!.Tipo);
            Assert.False(estado.Exitoso);
            Assert.Equal(TipoError.Conflicto, estado.Error!.Tipo);
        }

        var guardado = await Ayudas.LeerUsuarioAsync(fabrica, correoUnico);
        Assert.Equal(Roles.Administrador, guardado.Rol);
        Assert.True(guardado.Activo);
        Assert.Equal(selloInicial, guardado.SelloSeguridad);
        Assert.Contains(fabrica.Logs.Mensajes, m => m.Contains("admin_rol_rechazado motivo=ultimo_administrador", StringComparison.Ordinal));
        Assert.Contains(fabrica.Logs.Mensajes, m => m.Contains("admin_estado_rechazado motivo=ultimo_administrador", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AdministradorInactivo_SePuedeDegradar_AunqueQuedeUnSoloActivo()
    {
        await using var fabrica = new ClimaPlusApiFactory();
        var correoActivo = await Ayudas.CrearAdministradorAsync(fabrica);
        var correoInactivo = await Ayudas.CrearAdministradorAsync(fabrica);
        await Ayudas.ModificarUsuarioAsync(fabrica, correoInactivo, u => u.Activo = false);
        var idActivo = await AyudasDatos.IdUsuarioAsync(fabrica, correoActivo);
        var idInactivo = await AyudasDatos.IdUsuarioAsync(fabrica, correoInactivo);

        using var alcance = fabrica.Services.CreateScope();
        var servicio = alcance.ServiceProvider.GetRequiredService<UsuarioAdminService>();
        var resultado = await servicio.CambiarRolAsync(idActivo, idInactivo, Roles.Usuario, "127.0.0.1", default);

        Assert.True(resultado.Exitoso);
        Assert.Equal(Roles.Usuario, (await Ayudas.LeerUsuarioAsync(fabrica, correoInactivo)).Rol);
    }
}
