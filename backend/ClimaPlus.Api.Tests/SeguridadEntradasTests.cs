using System.Net;
using System.Net.Http.Json;
using ClimaPlus.Api.Tests.Infraestructura;

namespace ClimaPlus.Api.Tests;

/// <summary>Entradas maliciosas (XSS, inyección SQL, tamaños) y rate limiting.</summary>
public sealed class SeguridadEntradasTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly HttpClient _cliente;

    public SeguridadEntradasTests(ClimaPlusApiFactory fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("Ana\"><img src=x onerror=alert(1)>")]
    [InlineData("Robert'); DROP TABLE usuario;--")]
    public async Task Registro_rechaza_nombres_con_codigo(string nombre)
    {
        var respuesta = await Ayudas.RegistrarAsync(_cliente, Ayudas.CorreoUnico(), nombre: nombre);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("admin@ejemplo.com' OR '1'='1")]
    [InlineData("<script>@ejemplo.com")]
    public async Task Correos_maliciosos_se_rechazan_con_400(string correo)
    {
        var registro = await Ayudas.RegistrarAsync(_cliente, correo);
        var login = await Ayudas.LoginAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.BadRequest, registro.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
    }

    [Fact]
    public async Task Inyeccion_en_la_password_no_abre_sesion()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarAsync(_cliente, correo);

        var respuesta = await Ayudas.LoginAsync(_cliente, correo, "' OR '1'='1");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Campos_demasiado_largos_se_rechazan()
    {
        var respuesta = await Ayudas.RegistrarAsync(_cliente, Ayudas.CorreoUnico(), nombre: new string('a', 101));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Json_invalido_devuelve_400_sin_detalles_internos()
    {
        using var contenido = new StringContent("{ \"correo\": ", System.Text.Encoding.UTF8, "application/json");

        var respuesta = await _cliente.PostAsync("/api/auth/login", contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StackTrace", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", texto, StringComparison.Ordinal);
    }
}

/// <summary>Usa una fábrica propia con límite de 3 intentos por minuto.</summary>
public sealed class RateLimitingTests : IClassFixture<ClimaPlusApiFactoryLimiteBajo>
{
    private readonly HttpClient _cliente;

    public RateLimitingTests(ClimaPlusApiFactoryLimiteBajo fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Login_excede_el_limite_y_devuelve_429_con_Retry_After()
    {
        var correo = Ayudas.CorreoUnico();

        for (var intento = 1; intento <= ClimaPlusApiFactoryLimiteBajo.Limite; intento++)
        {
            var permitido = await _cliente.PostAsJsonAsync("/api/auth/login", new { correo, password = "Incorrecta2026" });
            Assert.Equal(HttpStatusCode.Unauthorized, permitido.StatusCode);
        }

        var bloqueado = await _cliente.PostAsJsonAsync("/api/auth/login", new { correo, password = "Incorrecta2026" });

        Assert.Equal((HttpStatusCode)429, bloqueado.StatusCode);
        Assert.True(bloqueado.Headers.Contains("Retry-After"));
        var problema = await Ayudas.LeerProblemaAsync(bloqueado);
        Assert.Equal(429, problema.GetProperty("status").GetInt32());
    }
}
