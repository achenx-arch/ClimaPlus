using System.Globalization;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Services.Alertas;

namespace ClimaPlus.Api.Tests.Alertas;

/// <summary>
/// Evaluación pura de reglas (Fase 2H): comparaciones estrictas, lluvia por código de OpenWeather,
/// prioridad de reglas, mensajes y ventana anti-duplicado. Sin base de datos ni HTTP.
/// </summary>
public sealed class EvaluadorAlertasTests
{
    private const long Coban = 10;
    private const long Salama = 20;
    private const short Nubes = 802;
    private const short LluviaLigera = 500;

    private static long _siguienteId;

    private static decimal D(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);

    private static ReglaEvaluable Regla(string tipo, decimal? umbral = null, long? idUbicacion = null, bool activa = true) =>
        new(Interlocked.Increment(ref _siguienteId), tipo, idUbicacion, umbral, activa);

    private static IReadOnlyList<AlertaPropuesta> Evaluar(decimal temperatura, short codigo, params ReglaEvaluable[] reglas) =>
        EvaluadorAlertas.Evaluar(temperatura, codigo, Coban, "Cobán", reglas);

    // ================================================================== Temperatura

    [Theory]
    [InlineData("32.01", "32", true)]
    [InlineData("33.4", "32", true)]
    [InlineData("32", "32", false)]      // igualdad exacta: no dispara (>)
    [InlineData("32.00", "32", false)]
    [InlineData("31.99", "32", false)]
    [InlineData("-4.99", "-5", true)]
    public void TemperaturaAlta_EsEstrictamenteMayor(string temperatura, string umbral, bool dispara)
    {
        var propuestas = Evaluar(D(temperatura), Nubes, Regla(TiposAlerta.TemperaturaAlta, D(umbral)));

        Assert.Equal(dispara, propuestas.Count == 1);
    }

    [Theory]
    [InlineData("9.99", "10", true)]
    [InlineData("-12.75", "0", true)]
    [InlineData("10", "10", false)]      // igualdad exacta: no dispara (<)
    [InlineData("10.01", "10", false)]
    [InlineData("-5", "-5", false)]
    public void TemperaturaBaja_EsEstrictamenteMenor(string temperatura, string umbral, bool dispara)
    {
        var propuestas = Evaluar(D(temperatura), Nubes, Regla(TiposAlerta.TemperaturaBaja, D(umbral)));

        Assert.Equal(dispara, propuestas.Count == 1);
    }

    [Fact]
    public void TemperaturaAlta_GuardaValorYUmbral_YMensajeDelMockup()
    {
        var alerta = Assert.Single(Evaluar(33.4m, Nubes, Regla(TiposAlerta.TemperaturaAlta, 32m)));

        Assert.Equal(TiposAlerta.TemperaturaAlta, alerta.Tipo);
        Assert.Equal(33.4m, alerta.ValorDetectado);
        Assert.Equal(32m, alerta.ValorUmbral);
        Assert.Equal("Cobán: 33.4 C supera tu umbral de 32 C", alerta.Mensaje);
    }

    [Fact]
    public void TemperaturaBaja_GuardaValorYUmbral_YMensaje()
    {
        var alerta = Assert.Single(Evaluar(-5.25m, Nubes, Regla(TiposAlerta.TemperaturaBaja, 0.5m)));

        Assert.Equal(TiposAlerta.TemperaturaBaja, alerta.Tipo);
        Assert.Equal(-5.25m, alerta.ValorDetectado);
        Assert.Equal(0.5m, alerta.ValorUmbral);
        Assert.Equal("Cobán: -5.25 C está por debajo de tu umbral de 0.5 C", alerta.Mensaje);
    }

    [Fact]
    public void SeComparaLaTemperaturaRecibida_NoOtraMedicion()
    {
        // El evaluador solo recibe la temperatura real (la sensación térmica nunca entra).
        Assert.Empty(Evaluar(31m, Nubes, Regla(TiposAlerta.TemperaturaAlta, 32m)));
        Assert.Single(Evaluar(32.5m, Nubes, Regla(TiposAlerta.TemperaturaAlta, 32m)));
    }

    [Theory]
    [InlineData("32", "32")]
    [InlineData("32.00", "32")]
    [InlineData("33.40", "33.4")]
    [InlineData("-12.75", "-12.75")]
    [InlineData("0", "0")]
    public void Numero_UsaPuntoDecimal_SinCerosSobrantes(string valor, string esperado)
    {
        var cultura = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-GT");   // la coma decimal del servidor no debe influir
            Assert.Equal(esperado, EvaluadorAlertas.Numero(D(valor)));
        }
        finally
        {
            CultureInfo.CurrentCulture = cultura;
        }
    }

    // ================================================================== Lluvia

    [Theory]
    [InlineData(200, true)]
    [InlineData(232, true)]
    [InlineData(299, true)]
    [InlineData(300, true)]
    [InlineData(321, true)]
    [InlineData(399, true)]
    [InlineData(500, true)]
    [InlineData(531, true)]
    [InlineData(599, true)]
    [InlineData(600, false)]
    [InlineData(622, false)]
    [InlineData(701, false)]
    [InlineData(781, false)]
    [InlineData(800, false)]
    [InlineData(804, false)]
    public void Lluvia_SoloGrupos2xx3xx5xx(int codigo, bool dispara)
    {
        Assert.Equal(dispara, EvaluadorAlertas.EsLluvia((short)codigo));
        Assert.Equal(dispara, Evaluar(20m, (short)codigo, Regla(TiposAlerta.Lluvia)).Count == 1);
    }

    [Fact]
    public void Lluvia_SinValores_YMensajeDelMockup()
    {
        var alerta = Assert.Single(Evaluar(20m, LluviaLigera, Regla(TiposAlerta.Lluvia)));

        Assert.Equal(TiposAlerta.Lluvia, alerta.Tipo);
        Assert.Null(alerta.ValorDetectado);
        Assert.Null(alerta.ValorUmbral);
        Assert.Equal("Lluvia detectada en Cobán", alerta.Mensaje);
    }

    // ================================================================== Prioridad de reglas

    [Fact]
    public void SinReglas_NoHayAlertas() => Assert.Empty(Evaluar(45m, LluviaLigera));

    [Fact]
    public void ReglaInactiva_NoDispara()
    {
        Assert.Empty(Evaluar(40m, LluviaLigera,
            Regla(TiposAlerta.TemperaturaAlta, 30m, activa: false),
            Regla(TiposAlerta.Lluvia, activa: false)));
    }

    [Fact]
    public void ReglaGlobal_AplicaACualquierUbicacion()
    {
        var alerta = Assert.Single(Evaluar(35m, Nubes, Regla(TiposAlerta.TemperaturaAlta, 30m)));

        Assert.Equal(30m, alerta.ValorUmbral);
    }

    [Fact]
    public void ReglaEspecifica_PrevaleceSobreGlobal()
    {
        var global = Regla(TiposAlerta.TemperaturaAlta, 30m);
        var especifica = Regla(TiposAlerta.TemperaturaAlta, 35m, Coban);

        // 33 supera la global pero no la específica: no hay alerta.
        Assert.Empty(Evaluar(33m, Nubes, global, especifica));

        var alerta = Assert.Single(Evaluar(36m, Nubes, especifica, global));
        Assert.Equal(35m, alerta.ValorUmbral);
        Assert.Equal(especifica, Assert.Single(EvaluadorAlertas.SeleccionarReglas(new[] { global, especifica }, Coban)));
    }

    [Fact]
    public void ReglaEspecificaInactiva_NoBloqueaLaGlobalActiva()
    {
        var global = Regla(TiposAlerta.TemperaturaAlta, 30m);
        var especificaPausada = Regla(TiposAlerta.TemperaturaAlta, 35m, Coban, activa: false);

        var alerta = Assert.Single(Evaluar(33m, Nubes, global, especificaPausada));

        Assert.Equal(30m, alerta.ValorUmbral);
    }

    [Fact]
    public void ReglaDeOtraUbicacion_NoAplica()
    {
        Assert.Empty(Evaluar(40m, LluviaLigera,
            Regla(TiposAlerta.TemperaturaAlta, 30m, Salama),
            Regla(TiposAlerta.Lluvia, idUbicacion: Salama)));
    }

    [Fact]
    public void LaPrioridadEsPorTipo()
    {
        // La específica de ALTA no afecta a la global de LLUVIA.
        var propuestas = Evaluar(40m, LluviaLigera,
            Regla(TiposAlerta.TemperaturaAlta, 35m, Coban),
            Regla(TiposAlerta.Lluvia));

        Assert.Equal(new[] { TiposAlerta.TemperaturaAlta, TiposAlerta.Lluvia }, propuestas.Select(p => p.Tipo).ToArray());
    }

    [Fact]
    public void AltaYBajaSimultaneas_GeneranAmbas()
    {
        // Global alta 20 °C y baja de Cobán 25 °C (combinación permitida, P-2): 22 °C cumple las dos (DP-9).
        var propuestas = Evaluar(22m, LluviaLigera,
            Regla(TiposAlerta.TemperaturaAlta, 20m),
            Regla(TiposAlerta.TemperaturaBaja, 25m, Coban),
            Regla(TiposAlerta.Lluvia, idUbicacion: Coban));

        Assert.Equal(
            new[] { TiposAlerta.TemperaturaAlta, TiposAlerta.TemperaturaBaja, TiposAlerta.Lluvia },
            propuestas.Select(p => p.Tipo).ToArray());
    }

    [Fact]
    public void Mensajes_NoSuperan255_ConElNombreMasLargo()
    {
        var nombre = new string('Á', 100);   // ubicacion.nombre es VARCHAR(100)

        var propuestas = EvaluadorAlertas.Evaluar(-49.99m, 300, Coban, nombre, new[]
        {
            Regla(TiposAlerta.TemperaturaAlta, -50m),
            Regla(TiposAlerta.TemperaturaBaja, 60m, Coban),
            Regla(TiposAlerta.Lluvia),
        });

        Assert.Equal(3, propuestas.Count);
        Assert.All(propuestas, p =>
        {
            Assert.True(p.Mensaje.Length <= EvaluadorAlertas.LongitudMaximaMensaje);
            Assert.Contains(nombre, p.Mensaje);
        });
    }

    // ================================================================== Ventana anti-duplicado (DP-8)

    [Theory]
    [InlineData(0, true)]
    [InlineData(59 * 60 * 1000, true)]
    [InlineData(60 * 60 * 1000 - 1, true)]   // 59:59.999
    [InlineData(60 * 60 * 1000, false)]      // 60:00 exactos: ya se permite
    [InlineData(61 * 60 * 1000, false)]
    public void Ventana_BloqueaMenosDe60Minutos(int milisegundosTranscurridos, bool bloquea)
    {
        var anterior = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(bloquea, MotorAlertas.BloqueaNuevaAlerta(anterior, anterior.AddMilliseconds(milisegundosTranscurridos)));
    }
}
