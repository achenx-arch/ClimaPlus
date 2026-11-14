using ClimaPlus.Api.Correo;

namespace ClimaPlus.Api.Tests.Correo;

/// <summary>Configuración SMTP: TLS obligatorio salvo con Mailpit o localhost.</summary>
public sealed class CorreoOptionsTests
{
    private static CorreoOptions Gmail(string seguridad = SeguridadCorreo.StartTls) => new()
    {
        Host = "smtp.gmail.com",
        Puerto = 587,
        Usuario = "climaplus@gmail.com",
        Password = "contraseña-de-aplicacion",
        Remitente = "climaplus@gmail.com",
        Seguridad = seguridad,
    };

    [Fact]
    public void SinConfigurar_EsValida_PeroNoEnvia()
    {
        var opciones = new CorreoOptions();

        Assert.False(opciones.Configurado);
        Assert.Empty(opciones.Validar());
    }

    [Fact]
    public void GmailConStartTls_EsValida() => Assert.Empty(Gmail().Validar());

    [Fact]
    public void ServidorExternoSinTls_SeRechaza() =>
        Assert.Contains(Gmail(SeguridadCorreo.Ninguna).Validar(), e => e.Contains("Ninguna", StringComparison.Ordinal));

    [Theory]
    [InlineData("mailpit")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public void SinTls_SoloConServidorLocal(string host)
    {
        var opciones = new CorreoOptions { Host = host, Puerto = 1025, Remitente = "no-reply@climaplus.local", Seguridad = SeguridadCorreo.Ninguna };

        Assert.Empty(opciones.Validar());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void PuertoFueraDeRango_SeRechaza(int puerto)
    {
        var opciones = Gmail();
        opciones.Puerto = puerto;

        Assert.NotEmpty(opciones.Validar());
    }

    [Fact]
    public void RemitenteInvalido_SeRechaza()
    {
        var opciones = Gmail();
        opciones.Remitente = "no es un correo";

        Assert.NotEmpty(opciones.Validar());
    }

    [Fact]
    public void SeguridadDesconocida_SeRechaza() => Assert.NotEmpty(Gmail("Tls13").Validar());
}
