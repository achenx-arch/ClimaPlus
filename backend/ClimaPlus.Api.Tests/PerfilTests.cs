using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Tests.Infraestructura;

namespace ClimaPlus.Api.Tests;

/// <summary>Perfil propio (M16 y M17, enmienda E-01).</summary>
public sealed class PerfilTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public PerfilTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Consultar_perfil_devuelve_rol_estado_y_fecha_sin_hash()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hash", texto, StringComparison.OrdinalIgnoreCase);
        var perfil = JsonSerializer.Deserialize<JsonElement>(texto);
        Assert.Equal(correo, perfil.GetProperty("correo").GetString());
        Assert.Equal(Roles.Usuario, perfil.GetProperty("rol").GetString());
        Assert.True(perfil.GetProperty("activo").GetBoolean());
        Assert.True(perfil.GetProperty("fechaCreacion").GetDateTime() <= DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Actualizar_perfil_cambia_nombre_y_correo_normalizados()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var nuevoCorreo = Ayudas.CorreoUnico();

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, "/api/usuarios/me", token,
            new { nombre = "  María   José  ", correo = "  " + nuevoCorreo.ToUpperInvariant() + " " }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var perfil = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("María José", perfil.GetProperty("nombre").GetString());
        Assert.Equal(nuevoCorreo, perfil.GetProperty("correo").GetString());

        // El token sigue siendo válido: identifica al usuario por id, no por correo.
        var me = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", token));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Actualizar_perfil_con_correo_de_otra_cuenta_devuelve_409()
    {
        var (correoOtro, _) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, "/api/usuarios/me", token,
            new { nombre = "Usuario Prueba", correo = correoOtro }));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Intento_de_modificar_rol_o_estado_devuelve_400_y_no_cambia_nada()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var conRol = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, "/api/usuarios/me", token,
            new { nombre = "Usuario Prueba", correo, rol = Roles.Administrador }));
        var conEstado = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, "/api/usuarios/me", token,
            new { nombre = "Usuario Prueba", correo, activo = false }));

        Assert.Equal(HttpStatusCode.BadRequest, conRol.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, conEstado.StatusCode);

        var guardado = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(Roles.Usuario, guardado.Rol);
        Assert.True(guardado.Activo);
    }

    [Fact]
    public async Task Cambiar_password_exige_la_actual_y_luego_solo_vale_la_nueva()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        const string nueva = "NuevaClave2027";

        var conActualMala = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, "/api/usuarios/me/password", token,
            new { passwordActual = "Incorrecta2026", passwordNueva = nueva }));
        Assert.Equal(HttpStatusCode.BadRequest, conActualMala.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(conActualMala)).GetProperty("errors").TryGetProperty("passwordActual", out _));

        var igualALaActual = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, "/api/usuarios/me/password", token,
            new { passwordActual = Ayudas.PasswordValida, passwordNueva = Ayudas.PasswordValida }));
        Assert.Equal(HttpStatusCode.BadRequest, igualALaActual.StatusCode);

        var correcta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, "/api/usuarios/me/password", token,
            new { passwordActual = Ayudas.PasswordValida, passwordNueva = nueva }));
        Assert.Equal(HttpStatusCode.NoContent, correcta.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.LoginAsync(_cliente, correo, nueva)).StatusCode);
    }

    [Fact]
    public async Task No_existe_ruta_para_leer_el_perfil_de_otro_usuario()
    {
        var (correoOtro, _) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var otro = await Ayudas.LeerUsuarioAsync(_fabrica, correoOtro);
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, $"/api/usuarios/{otro.IdUsuario}", token));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
