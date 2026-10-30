using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Services.Alertas;

namespace ClimaPlus.Api.Tests.Alertas;

/// <summary>Fase 7: evaluación pura de una lectura del ESP32 (RF-24, RN-BD-31, RN-BD-33, D-08).</summary>
public sealed class EvaluadorAlertasLecturaTests
{
    private const long Ubicacion = 7;

    private static readonly ReglaEvaluable[] Reglas =
    {
        new(1, TiposAlerta.TemperaturaAlta, Ubicacion, 30m, true),
        new(2, TiposAlerta.TemperaturaBaja, null, 5m, true),
        new(3, TiposAlerta.Lluvia, null, null, true),
    };

    [Fact]
    public void TemperaturaAlta_YLluvia_GeneranAmbas()
    {
        var propuestas = EvaluadorAlertas.EvaluarLectura(31.5m, true, Ubicacion, "Cobán", Reglas);

        Assert.Equal(new[] { TiposAlerta.TemperaturaAlta, TiposAlerta.Lluvia }, propuestas.Select(p => p.Tipo).ToArray());
        Assert.Equal(31.5m, propuestas[0].ValorDetectado);
        Assert.Equal(30m, propuestas[0].ValorUmbral);
        Assert.Null(propuestas[1].ValorDetectado);
        Assert.Equal("Lluvia detectada en Cobán", propuestas[1].Mensaje);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void SinLluvia_NoGeneraAlertaDeLluvia(bool? lluvia)
    {
        Assert.Empty(EvaluadorAlertas.EvaluarLectura(20m, lluvia, Ubicacion, "Cobán", Reglas));
    }

    [Fact]
    public void SinTemperatura_SoloSeEvaluaLaLluvia()
    {
        var propuestas = EvaluadorAlertas.EvaluarLectura(null, true, Ubicacion, "Cobán", Reglas);

        Assert.Equal(TiposAlerta.Lluvia, Assert.Single(propuestas).Tipo);
    }

    [Fact]
    public void TemperaturaBaja_ConReglaGlobal()
    {
        var propuesta = Assert.Single(EvaluadorAlertas.EvaluarLectura(-2.25m, false, Ubicacion, "Cobán", Reglas));

        Assert.Equal(TiposAlerta.TemperaturaBaja, propuesta.Tipo);
        Assert.Equal("Cobán: -2.25 C está por debajo de tu umbral de 5 C", propuesta.Mensaje);
    }

    [Fact]
    public void IgualAlUmbral_NoDispara()
    {
        Assert.Empty(EvaluadorAlertas.EvaluarLectura(30m, false, Ubicacion, "Cobán", Reglas));
    }

    [Fact]
    public void ReglaDeOtraUbicacion_NoAplica()
    {
        Assert.Empty(EvaluadorAlertas.EvaluarLectura(40m, false, 99, "Otra", new[] { new ReglaEvaluable(1, TiposAlerta.TemperaturaAlta, Ubicacion, 30m, true) }));
    }

    [Fact]
    public void MismaMedicion_MismoResultadoQueLaConsultaActual()
    {
        // Una lectura con lluvia equivale a una consulta con código 500 (lluvia ligera): mismas alertas y mensajes.
        var consulta = EvaluadorAlertas.Evaluar(31m, 500, Ubicacion, "Cobán", Reglas);
        var lectura = EvaluadorAlertas.EvaluarLectura(31m, true, Ubicacion, "Cobán", Reglas);

        Assert.Equal(consulta, lectura);
    }
}
