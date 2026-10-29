using System.Security.Cryptography;
using System.Text;
using ClimaPlus.Api.Security;

namespace ClimaPlus.Api.Tests.Dispositivos;

/// <summary>Fase 7: generación y hash de la API Key del ESP32 (RN-BD-28, 07-seguridad-inicial.md sección 5).</summary>
public sealed class ApiKeyDispositivoTests
{
    [Fact]
    public void Generar_ProduceClavesDe47CaracteresSeguras_EnUrl_YNoRepetidas()
    {
        var claves = Enumerable.Range(0, 200).Select(_ => ApiKeyDispositivo.Generar()).ToList();

        Assert.All(claves, c =>
        {
            Assert.StartsWith("cpk_", c);
            Assert.Equal(47, c.Length);
            Assert.Matches("^cpk_[A-Za-z0-9_-]{43}$", c);
        });
        Assert.Equal(claves.Count, claves.Distinct().Count());
    }

    [Fact]
    public void Hash_EsSha256HexadecimalMinuscula()
    {
        const string clave = "cpk_ejemplo";
        var esperado = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clave))).ToLowerInvariant();

        Assert.Equal(esperado, ApiKeyDispositivo.Hash(clave));
        Assert.Matches("^[0-9a-f]{64}$", ApiKeyDispositivo.Hash(clave));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Bearer abc", false)]
    [InlineData("cpk_abc", true)]
    public void FormatoValido(string? clave, bool esperado)
    {
        Assert.Equal(esperado, ApiKeyDispositivo.FormatoValido(clave));
    }

    [Fact]
    public void FormatoValido_RechazaClavesMuyLargas()
    {
        Assert.False(ApiKeyDispositivo.FormatoValido("cpk_" + new string('a', 125)));
    }
}
