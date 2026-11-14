using System.Net;
using System.Net.Sockets;
using System.Text;
using ClimaPlus.Api.Correo;
using ClimaPlus.Api.Tests.Infraestructura;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClimaPlus.Api.Tests.Correo;

/// <summary>
/// Diagnóstico del SMTP: cada fallo indica la etapa (Conectar, Autenticar, Enviar) y una descripción técnica segura
/// de la cadena de excepciones, sin contraseñas, códigos, asuntos, cuerpos ni direcciones. Usa un servidor SMTP falso
/// local (sin Internet ni TLS).
/// </summary>
public sealed class DiagnosticoSmtpTests
{
    private const string Password = "contrasena-que-no-debe-aparecer";
    private const string Codigo = "918273";
    private const string Destino = "persona.privada@ejemplo.com";

    private static MensajeCorreo Mensaje() =>
        new(Destino, $"Asunto {Codigo}", $"<p>{Codigo}</p>", $"Código {Codigo}", "codigo_verificar_correo");

    private static CorreoOptions Opciones(int puerto, string usuario = "", bool diagnostico = true) => new()
    {
        Host = "127.0.0.1",
        Puerto = puerto,
        Remitente = "no-reply@climaplus.local",
        Seguridad = SeguridadCorreo.Ninguna,
        Usuario = usuario,
        Password = usuario.Length > 0 ? Password : string.Empty,
        TimeoutSegundos = 5,
        Diagnostico = diagnostico,
    };

    private static async Task<(ErrorEnvioCorreoException Error, string Logs)> EnviarYCapturarAsync(CorreoOptions opciones)
    {
        var logs = new LogsCapturados();
        using var fabrica = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        var enviador = new SmtpEmailSender(new MonitorFijo<CorreoOptions>(opciones), fabrica);
        var error = await Assert.ThrowsAsync<ErrorEnvioCorreoException>(() => enviador.EnviarAsync(Mensaje(), CancellationToken.None));
        return (error, string.Join("\n", logs.Mensajes));
    }

    private static void SinDatosSensibles(string texto)
    {
        Assert.DoesNotContain(Password, texto);
        Assert.DoesNotContain(Codigo, texto);
        Assert.DoesNotContain(Destino, texto);
    }

    [Fact]
    public async Task ServidorQueCierraTrasEhlo_FallaEnConectar_ConSmtpProtocolException()
    {
        await using var servidor = new SmtpFalso(async (leer, escribir) =>
        {
            await escribir.WriteLineAsync("220 falso ESMTP");
            await leer.ReadLineAsync();   // EHLO
            // Cierra la conexión sin responder, como cuando algo intermedio corta la sesión.
        });

        var (error, logs) = await EnviarYCapturarAsync(Opciones(servidor.Puerto));

        Assert.Equal(EtapaSmtp.Conectar, error.Etapa);
        Assert.IsType<SmtpProtocolException>(error.InnerException);
        Assert.Contains("SmtpProtocolException[", DiagnosticoCorreo.Describir(error));
        Assert.Contains("combinacion_puerto_tls_habitual=True", logs);
        SinDatosSensibles(logs + DiagnosticoCorreo.Describir(error));
    }

    [Fact]
    public async Task CredencialesRechazadas_FallaEnAutenticar_SinRegistrarLaContrasena()
    {
        await using var servidor = new SmtpFalso(async (leer, escribir) =>
        {
            await escribir.WriteLineAsync("220 falso ESMTP");
            await leer.ReadLineAsync();   // EHLO
            await escribir.WriteLineAsync("250-falso");
            await escribir.WriteLineAsync("250 AUTH PLAIN");
            var auth = await leer.ReadLineAsync() ?? string.Empty;
            if (auth.Trim().Equals("AUTH PLAIN", StringComparison.OrdinalIgnoreCase))
            {
                await escribir.WriteLineAsync("334 ");
                await leer.ReadLineAsync();
            }

            await escribir.WriteLineAsync("535 5.7.8 Username and Password not accepted");
            await leer.ReadLineAsync();   // QUIT u otro comando, si llega
        });

        var (error, logs) = await EnviarYCapturarAsync(Opciones(servidor.Puerto, usuario: "cuenta.envio@ejemplo.com"));

        Assert.Equal(EtapaSmtp.Autenticar, error.Etapa);
        Assert.IsType<MailKit.Security.AuthenticationException>(error.InnerException);
        Assert.Contains("credenciales_rechazadas", DiagnosticoCorreo.Describir(error));
        Assert.Contains("etapa=Conectar resultado=ok", logs);
        Assert.Contains("autenticacion=PLAIN", logs);
        Assert.Contains("password_definida=True", logs);
        Assert.DoesNotContain("cuenta.envio@ejemplo.com", logs);
        SinDatosSensibles(logs + DiagnosticoCorreo.Describir(error));
    }

    [Fact]
    public async Task RemitenteRechazado_FallaEnEnviar_ConElCodigoSmtp()
    {
        await using var servidor = new SmtpFalso(async (leer, escribir) =>
        {
            await escribir.WriteLineAsync("220 falso ESMTP");
            await leer.ReadLineAsync();   // EHLO
            await escribir.WriteLineAsync("250 falso");
            await leer.ReadLineAsync();   // MAIL FROM
            await escribir.WriteLineAsync("550 5.7.1 Sender not allowed");
            for (var i = 0; i < 3; i++)
            {
                var linea = await leer.ReadLineAsync();
                if (linea is null) break;
                await escribir.WriteLineAsync(linea.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase) ? "221 adios" : "250 ok");
            }
        });

        var (error, logs) = await EnviarYCapturarAsync(Opciones(servidor.Puerto));

        Assert.Equal(EtapaSmtp.Enviar, error.Etapa);
        var comando = Assert.IsType<SmtpCommandException>(error.InnerException);
        Assert.Equal(550, (int)comando.StatusCode);
        Assert.Contains("estado_smtp=550", DiagnosticoCorreo.Describir(error));
        Assert.DoesNotContain("Sender not allowed", DiagnosticoCorreo.Describir(error));
        SinDatosSensibles(logs + DiagnosticoCorreo.Describir(error));
    }

    [Fact]
    public async Task PuertoSinServidor_FallaEnConectar()
    {
        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        escucha.Stop();

        var (error, _) = await EnviarYCapturarAsync(Opciones(puerto, diagnostico: false));

        Assert.Equal(EtapaSmtp.Conectar, error.Etapa);
        Assert.NotNull(error.InnerException);
    }

    [Fact]
    public void Describir_RecorreLaCadenaDeExcepciones_SinElTextoDeLosMensajes()
    {
        var interna = new SocketException((int)SocketError.ConnectionReset);
        var io = new IOException("respuesta con " + Destino, interna);
        var protocolo = new SmtpProtocolException("The SMTP server has unexpectedly disconnected.", io);
        var envuelta = new ErrorEnvioCorreoException(EtapaSmtp.Conectar, protocolo);

        var descripcion = DiagnosticoCorreo.Describir(envuelta);

        Assert.Equal("SmtpProtocolException", DiagnosticoCorreo.TipoPrincipal(envuelta));
        Assert.Equal(EtapaSmtp.Conectar, DiagnosticoCorreo.Etapa(envuelta));
        Assert.StartsWith("SmtpProtocolException[desconexion_inesperada] <- IOException[hresult=0x", descripcion);
        Assert.EndsWith("SocketException[socket=ConnectionReset]", descripcion);
        Assert.DoesNotContain(Destino, descripcion);
    }

    [Fact]
    public async Task ConfiguracionInusual_PuertoYModoTls_SeSenalaEnElDiagnostico()
    {
        // 465 espera TLS desde el primer byte (SslAlConectar); con StartTls el diagnóstico lo advierte. No hay servidor
        // en 127.0.0.1:465, así que el envío falla en Conectar, pero antes deja la advertencia.
        var opciones = Opciones(465);
        opciones.Seguridad = SeguridadCorreo.StartTls;

        var (error, logs) = await EnviarYCapturarAsync(opciones);

        Assert.Equal(EtapaSmtp.Conectar, error.Etapa);
        Assert.Contains("combinacion_puerto_tls_habitual=False", logs);
    }

    private sealed class MonitorFijo<T> : IOptionsMonitor<T>
    {
        public MonitorFijo(T valor) => CurrentValue = valor;

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    /// <summary>Servidor SMTP mínimo para una sola conexión, guiado por un script.</summary>
    private sealed class SmtpFalso : IAsyncDisposable
    {
        private readonly TcpListener _escucha = new(IPAddress.Loopback, 0);
        private readonly Task _tarea;

        public SmtpFalso(Func<StreamReader, StreamWriter, Task> guion)
        {
            _escucha.Start();
            Puerto = ((IPEndPoint)_escucha.LocalEndpoint).Port;
            _tarea = Task.Run(async () =>
            {
                try
                {
                    using var conexion = await _escucha.AcceptTcpClientAsync();
                    using var red = conexion.GetStream();
                    using var leer = new StreamReader(red, Encoding.ASCII);
                    using var escribir = new StreamWriter(red, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
                    await guion(leer, escribir);
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (SocketException)
                {
                }
            });
        }

        public int Puerto { get; }

        public async ValueTask DisposeAsync()
        {
            _escucha.Stop();
            try
            {
                await _tarea.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
            }
        }
    }
}
