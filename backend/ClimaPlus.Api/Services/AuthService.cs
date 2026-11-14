using ClimaPlus.Api.Correo;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Auth;
using ClimaPlus.Api.DTOs.Usuarios;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Autenticacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ClimaPlus.Api.Services;

/// <summary>
/// Registro con verificación de correo e inicio de sesión en dos pasos con código por correo (RF-01, RF-02;
/// autenticación segura, fases 2 y 3). La recuperación de contraseña (fase 4) está en AuthService.Recuperacion.cs y
/// Google (fase 5) en AuthService.Google.cs.
/// </summary>
public sealed partial class AuthService
{
    // Hash de una contraseña ficticia: se verifica cuando el correo no existe, para que
    // la respuesta tarde lo mismo y no revele qué correos están registrados.
    private static readonly Lazy<string> HashFicticio =
        new(() => new PasswordHasher<Usuario>().HashPassword(new Usuario(), "climaplus-hash-ficticio-2026"));

    private readonly ClimaPlusDbContext _db;
    private readonly IPasswordHasher<Usuario> _hasher;
    private readonly JwtTokenService _tokens;
    private readonly LimitadorIntentosLogin _intentos;
    private readonly ServicioDesafios _desafios;
    private readonly TokenDesafio _tokensDesafio;
    private readonly ControlEnvios _envios;
    private readonly IColaCorreos _cola;
    private readonly IOptions<CodigosOptions> _opcionesCodigos;
    private readonly IValidadorTokenGoogle _google;
    private readonly NoncesGoogle _nonces;
    private readonly IOptions<GoogleOptions> _opcionesGoogle;
    private readonly ILogger _log;

    public AuthService(
        ClimaPlusDbContext db, IPasswordHasher<Usuario> hasher, JwtTokenService tokens, LimitadorIntentosLogin intentos,
        ServicioDesafios desafios, TokenDesafio tokensDesafio, ControlEnvios envios, IColaCorreos cola,
        IOptions<CodigosOptions> opcionesCodigos, IValidadorTokenGoogle google, NoncesGoogle nonces,
        IOptions<GoogleOptions> opcionesGoogle, ILoggerFactory logs)
    {
        _google = google;
        _nonces = nonces;
        _opcionesGoogle = opcionesGoogle;
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _intentos = intentos;
        _desafios = desafios;
        _tokensDesafio = tokensDesafio;
        _envios = envios;
        _cola = cola;
        _opcionesCodigos = opcionesCodigos;
        _log = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    /// <summary>
    /// RF-01 con verificación de correo. Crea la cuenta sin verificar y envía un código VERIFICAR_CORREO. Si el correo
    /// ya existe, responde exactamente igual (202 con un desafío señuelo) y solo avisa al dueño del buzón: no revela
    /// qué correos están registrados (condición 3).
    /// </summary>
    public async Task<Resultado<RespuestaDesafio>> RegistrarAsync(RegistroRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var correo = Normalizacion.Correo(solicitud.Correo);

        var existente = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Correo == correo, cancelacion);
        if (existente is not null)
        {
            return Resultado<RespuestaDesafio>.Ok(RegistroDuplicado(existente, solicitud.Password, ip));
        }

        var usuario = new Usuario
        {
            Nombre = Normalizacion.Nombre(solicitud.Nombre),
            Correo = correo,
            Rol = Roles.Usuario,          // RN-BD-05: el registro público siempre es USUARIO.
            Activo = true,
            CorreoVerificado = false,
            FechaCreacion = DateTime.UtcNow,
        };
        usuario.PasswordHash = _hasher.HashPassword(usuario, solicitud.Password);

        _db.Usuarios.Add(usuario);
        try
        {
            await _db.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
        {
            // Dos registros simultáneos con el mismo correo: lo resuelve uq_usuario_correo y se responde como duplicado.
            _db.ChangeTracker.Clear();
            var ganador = await _db.Usuarios.AsNoTracking().SingleAsync(u => u.Correo == correo, cancelacion);
            return Resultado<RespuestaDesafio>.Ok(RegistroDuplicado(ganador, password: null, ip));
        }

        var desafio = await _desafios.CrearAsync(usuario, PropositosDesafio.VerificarCorreo, cancelacion);
        EnviarCodigo(usuario, desafio);

        _log.LogInformation("Seguridad: registro_exitoso idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
        return Resultado<RespuestaDesafio>.Ok(Respuesta(desafio, correo));
    }

    /// <summary>Correo ya registrado: aviso al dueño (máximo 1 por hora) y desafío señuelo con la misma forma.</summary>
    private RespuestaDesafio RegistroDuplicado(Usuario existente, string? password, string ip)
    {
        if (password is not null)
        {
            // Mismo trabajo que un registro real (calcular un hash), para no delatar el duplicado por el tiempo.
            _hasher.HashPassword(new Usuario(), password);
        }

        if (_envios.PermitirAviso(existente.IdUsuario, "registro_existente"))
        {
            _cola.Encolar(PlantillasCorreo.AvisoRegistroExistente(existente.Correo, existente.Nombre));
        }

        _log.LogInformation("Seguridad: registro_duplicado correo={Correo} ip={Ip}", RegistroSeguridad.EnmascararCorreo(existente.Correo), ip);
        return Respuesta(_desafios.CrearSenuelo(PropositosDesafio.VerificarCorreo), existente.Correo);
    }

    /// <summary>
    /// Confirma el correo con el código VERIFICAR_CORREO. No inicia sesión: el usuario va al login después.
    /// Un código incorrecto, vencido, usado, agotado o de un señuelo responde el mismo 400.
    /// </summary>
    public async Task<Resultado> VerificarCorreoAsync(VerificarCodigoRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var datos = await _tokensDesafio.LeerAsync(solicitud.Desafio);
        var idDesafio = datos?.Proposito == PropositosDesafio.VerificarCorreo ? datos.IdDesafio : Guid.NewGuid();

        // Tope por cuenta (condición 2): 10 códigos incorrectos por hora para este propósito. Al llegar, la respuesta
        // es el mismo 400 y no se valida nada; no afecta al login del dueño.
        var titular = await _desafios.BuscarTitularAsync(idDesafio, PropositosDesafio.VerificarCorreo, cancelacion);
        if (titular is not null && _envios.FallosAgotados(titular.IdUsuario, PropositosDesafio.VerificarCorreo))
        {
            _log.LogWarning("Seguridad: codigo_rechazado motivo=tope_fallos_cuenta proposito={Proposito} idUsuario={IdUsuario} ip={Ip}",
                PropositosDesafio.VerificarCorreo, titular.IdUsuario, ip);
            return Resultado.Falla(CodigoInvalido());
        }

        var desafio = await _desafios.VerificarAsync(idDesafio, PropositosDesafio.VerificarCorreo, solicitud.Codigo,
            EstadosDesafio.Consumido, cancelacion,
            alAceptar: d => d.Usuario.CorreoVerificado = true,
            alFallar: d => _envios.RegistrarFallo(d.IdUsuario, PropositosDesafio.VerificarCorreo));
        if (desafio is null)
        {
            _log.LogWarning("Seguridad: codigo_rechazado proposito={Proposito} ip={Ip}", PropositosDesafio.VerificarCorreo, ip);
            return Resultado.Falla(CodigoInvalido());
        }

        _log.LogInformation("Seguridad: correo_verificado idUsuario={IdUsuario} ip={Ip}", desafio.IdUsuario, ip);
        return Resultado.Ok();
    }

    /// <summary>
    /// Reenvía un código nuevo (invalida el anterior): uno cada 60 s y como máximo 5 por desafío. Para un señuelo, o
    /// un desafío ya usado o agotado, responde igual pero no envía nada.
    /// </summary>
    public async Task<Resultado<RespuestaReenvio>> ReenviarCodigoAsync(ReenviarCodigoRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var datos = await _tokensDesafio.LeerAsync(solicitud.Desafio);
        if (datos is null || !PropositosDesafio.Todos.Contains(datos.Proposito))
        {
            return Resultado<RespuestaReenvio>.Falla(new ErrorServicio(TipoError.Validacion,
                "El código venció o el enlace ya no es válido. Vuelve a empezar.", "desafio"));
        }

        switch (_envios.IntentarReenvio(datos.IdDesafio, out var esperar))
        {
            case EstadoReenvio.Esperar:
                return Resultado<RespuestaReenvio>.Falla(new ErrorServicio(TipoError.DemasiadosIntentos,
                    "Espera un momento antes de pedir otro código.", ReintentarSegundos: esperar));
            case EstadoReenvio.Agotado:
                return Resultado<RespuestaReenvio>.Falla(new ErrorServicio(TipoError.DemasiadosIntentos,
                    "Ya pediste demasiados códigos. Vuelve a empezar en unos minutos.", ReintentarSegundos: esperar));
        }

        var regenerado = await _desafios.RegenerarCodigoAsync(datos.IdDesafio, datos.Proposito, cancelacion);
        DateTime expira;
        if (regenerado is { } real)
        {
            EnviarCodigo(real.Desafio.Usuario, real.Emitido);
            expira = real.Emitido.ExpiraEn;
            _log.LogInformation("Seguridad: codigo_reenviado proposito={Proposito} idUsuario={IdUsuario} ip={Ip}",
                datos.Proposito, real.Desafio.IdUsuario, ip);
        }
        else
        {
            expira = _desafios.CrearSenuelo(datos.Proposito).ExpiraEn;
        }

        return Resultado<RespuestaReenvio>.Ok(new RespuestaReenvio(
            _tokensDesafio.Crear(datos.IdDesafio, datos.Proposito, expira, datos.ParaSesion, datos.CorreoExterno), datos.Proposito, expira,
            _opcionesCodigos.Value.SegundosEntreReenvios));
    }

    /// <summary>Encola el correo con el código, salvo que la cuenta ya recibió su tope de códigos esta hora.</summary>
    private void EnviarCodigo(Usuario usuario, DesafioEmitido desafio)
    {
        if (!_envios.PermitirCodigo(usuario.IdUsuario))
        {
            _log.LogWarning("Seguridad: codigo_no_enviado motivo=tope_por_cuenta idUsuario={IdUsuario}", usuario.IdUsuario);
            return;
        }

        _cola.Encolar(PlantillasCorreo.Codigo(usuario.Correo, usuario.Nombre, desafio.Proposito, desafio.Codigo,
            _desafios.MinutosVigencia(desafio.Proposito)));
    }

    /// <summary>Token del desafío + datos para la pantalla del código. Igual para reales y señuelos.</summary>
    private RespuestaDesafio Respuesta(DesafioEmitido desafio, string correo, bool paraSesion = false, string? correoExterno = null)
    {
        _envios.RegistrarEmision(desafio.IdDesafio);
        return new RespuestaDesafio(
            _tokensDesafio.Crear(desafio.IdDesafio, desafio.Proposito, desafio.ExpiraEn, paraSesion, correoExterno),
            desafio.Proposito,
            RegistroSeguridad.EnmascararCorreo(correo),
            desafio.ExpiraEn,
            _opcionesCodigos.Value.SegundosEntreReenvios);
    }

    private static ErrorServicio CodigoInvalido() =>
        new(TipoError.Validacion, "El código no es válido o ya venció. Revisa tu correo o pide uno nuevo.", "codigo");

    /// <summary>
    /// Paso 1 del inicio de sesión (RF-02 con doble verificación): comprueba correo y contraseña y, si son correctos,
    /// envía un código al correo y responde 202 con el desafío, nunca con el JWT. Si la cuenta aún no verificó su
    /// correo (cuentas anteriores a la migración 004 o registros sin terminar), el código es VERIFICAR_CORREO: al
    /// validarlo en el paso 2 se verifica el correo y se inicia sesión.
    /// </summary>
    public async Task<Resultado<RespuestaDesafio>> IniciarSesionAsync(LoginRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var correo = Normalizacion.Correo(solicitud.Correo);

        // Bloqueo temporal por cuenta + IP y por cuenta (condición 2). Se comprueba antes de buscar al usuario y vale
        // igual para correos inexistentes, así no revela qué cuentas existen.
        if (Bloqueado(correo, ip) is { } bloqueo)
        {
            return Resultado<RespuestaDesafio>.Falla(bloqueo);
        }

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.Correo == correo, cancelacion);

        // Migración 004: una cuenta solo de Google (sin contraseña local) responde igual que un correo inexistente.
        if (usuario?.PasswordHash is null)
        {
            _hasher.VerifyHashedPassword(new Usuario(), HashFicticio.Value, solicitud.Password);
            _intentos.RegistrarFallo(correo, ip);
            _log.LogWarning("Seguridad: login_fallido motivo={Motivo} correo={Correo} ip={Ip}",
                usuario is null ? "credenciales" : "sin_password_local", RegistroSeguridad.EnmascararCorreo(correo), ip);
            return Resultado<RespuestaDesafio>.Falla(CredencialesInvalidas());
        }

        var verificacion = _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, solicitud.Password);
        if (verificacion == PasswordVerificationResult.Failed)
        {
            _intentos.RegistrarFallo(correo, ip);
            _log.LogWarning("Seguridad: login_fallido motivo=credenciales idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            return Resultado<RespuestaDesafio>.Falla(CredencialesInvalidas());
        }

        // Solo después de comprobar la contraseña se informa que la cuenta está desactivada (RN-BD-07).
        if (!usuario.Activo)
        {
            _log.LogWarning("Seguridad: login_rechazado motivo=cuenta_desactivada idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            return Resultado<RespuestaDesafio>.Falla(CuentaDesactivada());
        }

        if (verificacion == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.PasswordHash = _hasher.HashPassword(usuario, solicitud.Password);
            await _db.SaveChangesAsync(cancelacion);
        }

        // El contador de fallos NO se reinicia aquí: los códigos incorrectos del paso 2 también suman, y solo un
        // inicio de sesión completo lo reinicia. Así, conocer la contraseña no permite probar códigos sin límite.
        var proposito = usuario.CorreoVerificado ? PropositosDesafio.InicioSesion : PropositosDesafio.VerificarCorreo;
        var desafio = await _desafios.CrearAsync(usuario, proposito, cancelacion);
        EnviarCodigo(usuario, desafio);

        _log.LogInformation("Seguridad: login_codigo_enviado proposito={Proposito} idUsuario={IdUsuario} ip={Ip}",
            proposito, usuario.IdUsuario, ip);
        return Resultado<RespuestaDesafio>.Ok(Respuesta(desafio, usuario.Correo, paraSesion: true));
    }

    /// <summary>
    /// Paso 2 del inicio de sesión: valida el código del desafío emitido por el paso 1 y solo entonces emite el JWT.
    /// Acepta INICIO_SESION, o VERIFICAR_CORREO si el paso 1 lo emitió para una cuenta sin verificar (y entonces
    /// marca el correo como verificado). Un desafío de registro, de otro flujo o alterado responde 400. Cada código
    /// incorrecto suma al bloqueo por cuenta + IP del login.
    /// </summary>
    public async Task<Resultado<LoginResponse>> VerificarInicioSesionAsync(VerificarCodigoRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var datos = await _tokensDesafio.LeerAsync(solicitud.Desafio);
        var valido = datos is { ParaSesion: true }
                     && datos.Proposito is PropositosDesafio.InicioSesion or PropositosDesafio.VerificarCorreo;
        var proposito = valido ? datos!.Proposito : PropositosDesafio.InicioSesion;
        var idDesafio = valido ? datos!.IdDesafio : Guid.NewGuid();

        var titular = await _desafios.BuscarTitularAsync(idDesafio, proposito, cancelacion);
        if (titular is not null && Bloqueado(titular.Correo, ip) is { } bloqueo)
        {
            return Resultado<LoginResponse>.Falla(bloqueo);
        }

        var desafio = await _desafios.VerificarAsync(idDesafio, proposito, solicitud.Codigo, EstadosDesafio.Consumido, cancelacion,
            alAceptar: d =>
            {
                if (d.Proposito == PropositosDesafio.VerificarCorreo)
                {
                    d.Usuario.CorreoVerificado = true;
                }
            },
            alFallar: d => _intentos.RegistrarFallo(d.Usuario.Correo, ip));
        if (desafio is null)
        {
            _log.LogWarning("Seguridad: codigo_rechazado proposito={Proposito} ip={Ip}", proposito, ip);
            return Resultado<LoginResponse>.Falla(CodigoInvalido());
        }

        var usuario = desafio.Usuario;
        _intentos.Reiniciar(usuario.Correo, ip);

        // La cuenta pudo desactivarse entre los dos pasos: no se emite sesión.
        if (!usuario.Activo)
        {
            _log.LogWarning("Seguridad: login_rechazado motivo=cuenta_desactivada idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            return Resultado<LoginResponse>.Falla(CuentaDesactivada());
        }

        if (proposito == PropositosDesafio.VerificarCorreo)
        {
            _log.LogInformation("Seguridad: correo_verificado idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
        }

        _log.LogInformation("Seguridad: login_exitoso metodo=password idUsuario={IdUsuario} rol={Rol} ip={Ip}", usuario.IdUsuario, usuario.Rol, ip);
        return Resultado<LoginResponse>.Ok(await EmitirSesionAsync(usuario, cancelacion));
    }

    /// <summary>JWT de ClimaPlus (con el sello de seguridad actual) y datos públicos del usuario.</summary>
    private async Task<LoginResponse> EmitirSesionAsync(Usuario usuario, CancellationToken cancelacion)
    {
        var token = _tokens.Crear(usuario);
        var tieneGoogle = await _db.IdentidadesExternas.AnyAsync(i => i.IdUsuario == usuario.IdUsuario, cancelacion);
        return new LoginResponse(token.Token, "Bearer", token.ExpiraEn, UsuarioResponse.Desde(usuario, tieneGoogle));
    }

    /// <summary>Error 429 si el correo está bloqueado desde esa IP (o desde todas), o null.</summary>
    private ErrorServicio? Bloqueado(string correo, string ip)
    {
        if (_intentos.TiempoBloqueo(correo, ip) is not { } bloqueo)
        {
            return null;
        }

        var segundos = Math.Max(1, (int)Math.Ceiling(bloqueo.TotalSeconds));
        _log.LogWarning("Seguridad: login_bloqueado correo={Correo} ip={Ip} segundos={Segundos}",
            RegistroSeguridad.EnmascararCorreo(correo), ip, segundos);
        return new ErrorServicio(TipoError.DemasiadosIntentos,
            "Demasiados intentos fallidos para esta cuenta. Espera unos minutos e inténtalo de nuevo.",
            ReintentarSegundos: segundos);
    }

    private static ErrorServicio CuentaDesactivada() =>
        new(TipoError.CuentaDesactivada, "Tu cuenta está desactivada. Contacta al administrador.");

    internal static bool EsViolacionDeUnicidad(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static ErrorServicio CredencialesInvalidas() =>
        new(TipoError.CredencialesInvalidas, "Correo o contraseña incorrectos.");
}
