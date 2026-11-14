using ClimaPlus.Api.Correo;
using ClimaPlus.Api.Domain.Constantes;

namespace ClimaPlus.Api.Tests.Correo;

/// <summary>Plantillas de correo: llevan el código, no llevan datos sensibles y escapan el nombre en HTML.</summary>
public sealed class PlantillasCorreoTests
{
    // Lista simple para LINQ; TheoryData<string> no implementa IEnumerable<string> en esta versión de xUnit.
    private static readonly string[] TodosLosPropositos =
    {
        PropositosDesafio.VerificarCorreo,
        PropositosDesafio.InicioSesion,
        PropositosDesafio.InicioGoogle,
        PropositosDesafio.VincularGoogle,
        PropositosDesafio.RecuperarPassword,
        PropositosDesafio.CambiarPassword,
    };

    public static TheoryData<string> Propositos
    {
        get
        {
            var datos = new TheoryData<string>();
            foreach (var proposito in TodosLosPropositos)
            {
                datos.Add(proposito);
            }

            return datos;
        }
    }

    [Theory]
    [MemberData(nameof(Propositos))]
    public void Codigo_LlevaElCodigoYLaVigencia_EnHtmlYTexto(string proposito)
    {
        var correo = PlantillasCorreo.Codigo("ana@ejemplo.com", "Ana", proposito, "482913", 10);

        Assert.Equal("ana@ejemplo.com", correo.Destinatario);
        Assert.Contains("ClimaPlus", correo.Asunto);
        Assert.Contains("482913", correo.Html);
        Assert.Contains("482913", correo.Texto);
        Assert.Contains("10 minutos", correo.Texto);
        Assert.StartsWith("codigo_", correo.Tipo);
        // El código nunca va en el asunto (se ve en las notificaciones del teléfono y en la bandeja).
        Assert.DoesNotContain("482913", correo.Asunto);
    }

    [Fact]
    public void Codigo_CadaPropositoTieneAsuntoYTipoPropios()
    {
        var correos = TodosLosPropositos.Select(p => PlantillasCorreo.Codigo("a@b.com", "A", p, "000111", 5)).ToList();

        Assert.Equal(correos.Count, correos.Select(c => c.Asunto).Distinct().Count());
        Assert.Equal(correos.Count, correos.Select(c => c.Tipo).Distinct().Count());
    }

    [Fact]
    public void Codigo_ListaDePropositos_CubreTodosLosDelDominio()
    {
        Assert.Equal(PropositosDesafio.Todos.Order(), TodosLosPropositos.Order());
    }

    [Fact]
    public void Codigo_CambiarPassword_AvisaQueSinElCodigoNoHayCambio()
    {
        var correo = PlantillasCorreo.Codigo("ana@ejemplo.com", "Ana", PropositosDesafio.CambiarPassword, "482913", 5);

        Assert.Equal("codigo_cambiar_password", correo.Tipo);
        Assert.Contains("cambio de tu contraseña", correo.Texto);
        Assert.Contains("5 minutos", correo.Texto);
        Assert.Contains("no cambiará sin él", correo.Texto);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public void Codigo_RechazaLoQueNoSeanSeisDigitos(string codigo) =>
        Assert.Throws<ArgumentException>(() => PlantillasCorreo.Codigo("a@b.com", "A", PropositosDesafio.InicioSesion, codigo, 5));

    [Fact]
    public void Codigo_RechazaUnPropositoDesconocido() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PlantillasCorreo.Codigo("a@b.com", "A", "OTRO", "123456", 5));

    [Fact]
    public void ElNombreSeEscapaEnHtml()
    {
        var correo = PlantillasCorreo.Codigo("a@b.com", "<script>alert(1)</script>", PropositosDesafio.InicioSesion, "123456", 5);

        Assert.DoesNotContain("<script>", correo.Html);
        Assert.Contains("&lt;script&gt;", correo.Html);
    }

    [Fact]
    public void Avisos_NoLlevanCodigosNiEnlaces()
    {
        var avisos = new[]
        {
            PlantillasCorreo.AvisoPasswordCambiada("a@b.com", "Ana", new DateTime(2026, 11, 9, 18, 5, 0, DateTimeKind.Utc)),
            PlantillasCorreo.AvisoRegistroExistente("a@b.com", "Ana"),
            PlantillasCorreo.AvisoGoogleVinculado("a@b.com", "Ana"),
            PlantillasCorreo.AvisoGoogleDesvinculado("a@b.com", "Ana"),
        };

        foreach (var aviso in avisos)
        {
            Assert.DoesNotContain("href", aviso.Html);
            Assert.DoesNotContain("http", aviso.Texto);
            Assert.StartsWith("aviso_", aviso.Tipo);
        }

        Assert.Contains("9 de noviembre de 2026, 18:05 UTC", avisos[0].Texto);
    }
}
