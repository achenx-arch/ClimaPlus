using System.Text.RegularExpressions;
using ClimaPlus.Api.Correo;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs;
using ClimaPlus.Api.DTOs.Auth;
using ClimaPlus.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services;

/// <summary>
/// Autenticación segura, fase 5: "Continuar con Google" con Google Identity Services (GIS).
///
/// Flujo: el navegador pide un nonce (GET /api/auth/google/iniciar), muestra el botón oficial de Google con ese nonce
/// y Google le entrega un ID token firmado; el navegador lo envía por POST a la API, que lo valida (firma, emisor,
/// audiencia, vencimiento, nonce de un solo uso, "sub") y entonces emite el JWT de ClimaPlus. Google es la prueba de
/// identidad: no se pide además el código por correo (decisión del 10-nov-2026). El código sí se exige para vincular.
///
/// Reglas:
/// - La identidad es el "sub" de Google. El correo nunca vincula cuentas por sí solo.
/// - Cuenta nueva: solo con email_verified = true, siempre rol USUARIO, sin contraseña local, correo verificado.
/// - Si el correo ya es de una cuenta de ClimaPlus: 409, no se crea otra ni se vincula. El dueño entra con su
///   contraseña y vincula desde el perfil (contraseña actual + código por correo, condición 1).
/// - Desvincular exige la contraseña actual y que exista una contraseña local: nadie se queda sin forma de entrar.
/// - Vincular y desvincular renuevan el sello de seguridad (cierran las demás sesiones) y devuelven una sesión nueva
///   para el dispositivo que hizo el cambio.
/// - La unicidad de (proveedor, sub) y (usuario, proveedor) la garantizan los índices únicos de identidad_externa; en
///   una sola instancia, además, un candado serializa la creación y la vinculación.
/// </summary>
public sealed partial class AuthService
{
    private static readonly SemaphoreSlim CandadoGoogle = new(1, 1);

    private static readonly Regex CorreoValido = new(ReglasValidacion.PatronCorreo, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    /// <summary>GET /api/auth/google/iniciar: Client ID y nonce de un solo uso (10 minutos).</summary>
    public Resultado<RespuestaInicioGoogle> IniciarGoogle(UsoNonceGoogle uso, long? idUsuario)
    {
        if (GoogleNoConfigurado() is { } error)
        {
            return Resultado<RespuestaInicioGoogle>.Falla(error);
        }

        if (_nonces.Emitir(uso, idUsuario) is not { } emitido)
        {
            return Resultado<RespuestaInicioGoogle>.Falla(new ErrorServicio(TipoError.DemasiadosIntentos,
                "Hay demasiadas solicitudes de inicio con Google. Inténtalo en unos minutos.", ReintentarSegundos: 60));
        }

        return Resultado<RespuestaInicioGoogle>.Ok(new RespuestaInicioGoogle(_opcionesGoogle.Value.ClientId.Trim(), emitido.Nonce, emitido.ExpiraEn));
    }

    /// <summary>
    /// POST /api/auth/google. Con un ID token válido: entra a la cuenta vinculada a ese "sub", o crea una cuenta
    /// nueva (USUARIO). Emite el JWT de ClimaPlus en la misma respuesta.
    /// </summary>
    public async Task<Resultado<LoginResponse>> IniciarSesionGoogleAsync(GoogleLoginRequest solicitud, string ip, CancellationToken cancelacion)
    {
        if (GoogleNoConfigurado() is { } noConfigurado)
        {
            return Resultado<LoginResponse>.Falla(noConfigurado);
        }

        var identidad = await _google.ValidarAsync(solicitud.Credencial, cancelacion);
        if (identidad is null || !_nonces.Consumir(identidad.Nonce, UsoNonceGoogle.Login, null))
        {
            if (identidad is not null)
            {
                _log.LogWarning("Seguridad: google_token_rechazado motivo=nonce ip={Ip}", ip);
            }

            return Resultado<LoginResponse>.Falla(GoogleInvalido());
        }

        await CandadoGoogle.WaitAsync(cancelacion);
        try
        {
            var vinculada = await _db.IdentidadesExternas
                .Include(i => i.Usuario)
                .SingleOrDefaultAsync(i => i.Proveedor == ProveedoresIdentidad.Google && i.Sujeto == identidad.Sujeto, cancelacion);

            if (vinculada is not null)
            {
                return await EntrarConIdentidadAsync(vinculada, identidad, ip, cancelacion);
            }

            if (!identidad.CorreoVerificado)
            {
                _log.LogWarning("Seguridad: google_login_rechazado motivo=correo_no_verificado ip={Ip}", ip);
                return Resultado<LoginResponse>.Falla(new ErrorServicio(TipoError.Validacion,
                    "Google no confirmó que este correo sea tuyo (correo sin verificar). Verifica tu correo en Google o regístrate con correo y contraseña.",
                    "credencial"));
            }

            if (identidad.Correo.Length > ReglasValidacion.CorreoMax || !CorreoValido.IsMatch(identidad.Correo))
            {
                _log.LogWarning("Seguridad: google_login_rechazado motivo=correo_no_admitido ip={Ip}", ip);
                return Resultado<LoginResponse>.Falla(new ErrorServicio(TipoError.Validacion,
                    "ClimaPlus no admite el correo de esta cuenta de Google. Regístrate con correo y contraseña.", "credencial"));
            }

            // Nunca se vincula ni se entra a una cuenta existente solo porque el correo coincide.
            if (await _db.Usuarios.AnyAsync(u => u.Correo == identidad.Correo, cancelacion))
            {
                _log.LogWarning("Seguridad: google_login_rechazado motivo=correo_de_cuenta_local correo={Correo} ip={Ip}",
                    RegistroSeguridad.EnmascararCorreo(identidad.Correo), ip);
                return Resultado<LoginResponse>.Falla(CorreoDeCuentaExistente());
            }

            var ahora = DateTime.UtcNow;
            var usuario = new Usuario
            {
                Nombre = NombreDesdeGoogle(identidad),
                Correo = identidad.Correo,
                Rol = Roles.Usuario,            // Google nunca concede ADMINISTRADOR.
                Activo = true,
                CorreoVerificado = true,        // email_verified = true, comprobado arriba.
                PasswordHash = null,            // cuenta solo de Google: puede crear contraseña en la fase 4.
                FechaCreacion = ahora,
            };
            usuario.IdentidadesExternas.Add(new IdentidadExterna
            {
                Proveedor = ProveedoresIdentidad.Google,
                Sujeto = identidad.Sujeto,
                CorreoProveedor = identidad.Correo,
                FechaVinculacion = ahora,
            });
            _db.Usuarios.Add(usuario);

            try
            {
                await _db.SaveChangesAsync(cancelacion);   // usuario e identidad en una sola transacción
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                // Otra instancia creó la cuenta o el correo a la vez: los índices únicos lo impidieron.
                _db.ChangeTracker.Clear();
                _log.LogWarning("Seguridad: google_login_rechazado motivo=duplicado ip={Ip}", ip);
                return Resultado<LoginResponse>.Falla(CorreoDeCuentaExistente());
            }

            _log.LogInformation("Seguridad: google_cuenta_creada idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            _log.LogInformation("Seguridad: login_exitoso metodo=google idUsuario={IdUsuario} rol={Rol} ip={Ip}", usuario.IdUsuario, usuario.Rol, ip);
            return Resultado<LoginResponse>.Ok(await EmitirSesionAsync(usuario, cancelacion));
        }
        finally
        {
            CandadoGoogle.Release();
        }
    }

    /// <summary>
    /// POST /api/usuarios/me/google/vincular. Paso 1: con sesión, contraseña actual e ID token de Google (nonce de
    /// vinculación de este usuario), envía un código VINCULAR_GOOGLE al correo de la cuenta. 202 con el desafío.
    /// </summary>
    public async Task<Resultado<RespuestaDesafio>> IniciarVinculacionGoogleAsync(
        long idUsuario, VincularGoogleRequest solicitud, string ip, CancellationToken cancelacion)
    {
        if (GoogleNoConfigurado() is { } noConfigurado)
        {
            return Resultado<RespuestaDesafio>.Falla(noConfigurado);
        }

        var usuario = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is null)
        {
            return Resultado<RespuestaDesafio>.Falla(new ErrorServicio(TipoError.NoEncontrado, "No se encontró la cuenta del usuario autenticado."));
        }

        if (await _db.IdentidadesExternas.AnyAsync(i => i.IdUsuario == idUsuario && i.Proveedor == ProveedoresIdentidad.Google, cancelacion))
        {
            return Resultado<RespuestaDesafio>.Falla(new ErrorServicio(TipoError.Conflicto,
                "Tu cuenta ya tiene una cuenta de Google vinculada. Desvincúlala primero si quieres usar otra."));
        }

        if (usuario.PasswordHash is null || !usuario.CorreoVerificado)
        {
            // Condición 1: una cuenta local sin verificar debe recuperar su contraseña primero (eso verifica el correo).
            return Resultado<RespuestaDesafio>.Falla(new ErrorServicio(TipoError.Conflicto,
                "Antes de vincular Google, verifica tu correo con \"¿Olvidaste tu contraseña?\"."));
        }

        if (VerificarPasswordActual(usuario, solicitud.PasswordActual, ip) is { } errorPassword)
        {
            return Resultado<RespuestaDesafio>.Falla(errorPassword);
        }

        var identidad = await _google.ValidarAsync(solicitud.Credencial, cancelacion);
        if (identidad is null || !_nonces.Consumir(identidad.Nonce, UsoNonceGoogle.Vincular, idUsuario))
        {
            _log.LogWarning("Seguridad: google_vinculacion_rechazada motivo=token idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
            return Resultado<RespuestaDesafio>.Falla(new ErrorServicio(TipoError.Validacion,
                "No se pudo validar tu cuenta de Google. Vuelve a intentarlo.", "credencial"));
        }

        if (await _db.IdentidadesExternas.AnyAsync(i => i.Proveedor == ProveedoresIdentidad.Google && i.Sujeto == identidad.Sujeto, cancelacion))
        {
            _log.LogWarning("Seguridad: google_vinculacion_rechazada motivo=sub_de_otra_cuenta idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
            return Resultado<RespuestaDesafio>.Falla(SubDeOtraCuenta());
        }

        var desafio = await _desafios.CrearAsync(usuario, PropositosDesafio.VincularGoogle, cancelacion, sujetoExterno: identidad.Sujeto);
        EnviarCodigo(usuario, desafio);

        var correoExterno = identidad.Correo.Length <= ReglasValidacion.CorreoMax ? identidad.Correo : identidad.Correo[..ReglasValidacion.CorreoMax];
        _log.LogInformation("Seguridad: google_vinculacion_codigo_enviado idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
        return Resultado<RespuestaDesafio>.Ok(Respuesta(desafio, usuario.Correo, correoExterno: correoExterno));
    }

    /// <summary>
    /// POST /api/usuarios/me/google/vincular/verificar. Paso 2: con el código del correo, crea la identidad de Google
    /// (sub del desafío, nunca del cliente), renueva el sello y devuelve una sesión nueva.
    /// </summary>
    public async Task<Resultado<LoginResponse>> VerificarVinculacionGoogleAsync(
        long idUsuario, VerificarCodigoRequest solicitud, string ip, CancellationToken cancelacion)
    {
        const string proposito = PropositosDesafio.VincularGoogle;
        var datos = await _tokensDesafio.LeerAsync(solicitud.Desafio);
        var idDesafio = datos?.Proposito == proposito && datos.CorreoExterno is not null ? datos.IdDesafio : Guid.NewGuid();

        // El desafío tiene que ser de la cuenta de esta sesión.
        var titular = await _desafios.BuscarTitularAsync(idDesafio, proposito, cancelacion);
        if (titular is null || titular.IdUsuario != idUsuario)
        {
            idDesafio = Guid.NewGuid();
        }
        else if (_envios.FallosAgotados(idUsuario, proposito))
        {
            return Resultado<LoginResponse>.Falla(CodigoInvalido());
        }

        await CandadoGoogle.WaitAsync(cancelacion);
        try
        {
            var sujeto = await _db.DesafiosAutenticacion.AsNoTracking()
                .Where(d => d.IdDesafio == idDesafio && d.Proposito == proposito)
                .Select(d => d.SujetoExterno)
                .SingleOrDefaultAsync(cancelacion);
            if (sujeto is not null)
            {
                if (await _db.IdentidadesExternas.AnyAsync(i => i.Proveedor == ProveedoresIdentidad.Google && i.Sujeto == sujeto, cancelacion))
                {
                    return Resultado<LoginResponse>.Falla(SubDeOtraCuenta());
                }

                if (await _db.IdentidadesExternas.AnyAsync(i => i.IdUsuario == idUsuario && i.Proveedor == ProveedoresIdentidad.Google, cancelacion))
                {
                    return Resultado<LoginResponse>.Falla(new ErrorServicio(TipoError.Conflicto, "Tu cuenta ya tiene una cuenta de Google vinculada."));
                }
            }

            DesafioAutenticacion? desafio;
            try
            {
                desafio = await _desafios.VerificarAsync(idDesafio, proposito, solicitud.Codigo, EstadosDesafio.Consumido, cancelacion,
                    alAceptar: d =>
                    {
                        d.Usuario.IdentidadesExternas.Add(new IdentidadExterna
                        {
                            Proveedor = ProveedoresIdentidad.Google,
                            Sujeto = d.SujetoExterno!,
                            CorreoProveedor = datos!.CorreoExterno!,
                            FechaVinculacion = DateTime.UtcNow,
                        });
                        d.Usuario.SelloSeguridad = Guid.NewGuid();   // cierra las demás sesiones
                    },
                    alFallar: d => _envios.RegistrarFallo(d.IdUsuario, proposito));
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                _db.ChangeTracker.Clear();
                return Resultado<LoginResponse>.Falla(SubDeOtraCuenta());
            }

            if (desafio is null)
            {
                _log.LogWarning("Seguridad: codigo_rechazado proposito={Proposito} ip={Ip}", proposito, ip);
                return Resultado<LoginResponse>.Falla(CodigoInvalido());
            }

            var usuario = desafio.Usuario;
            _cola.Encolar(PlantillasCorreo.AvisoGoogleVinculado(usuario.Correo, usuario.Nombre));
            _log.LogInformation("Seguridad: google_vinculado idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            return Resultado<LoginResponse>.Ok(await EmitirSesionAsync(usuario, cancelacion));
        }
        finally
        {
            CandadoGoogle.Release();
        }
    }

    /// <summary>
    /// POST /api/usuarios/me/google/desvincular. Con la contraseña actual, quita la identidad de Google. Una cuenta sin
    /// contraseña local recibe 409 (primero "Establecer contraseña"). Conserva todos los datos del usuario.
    /// </summary>
    public async Task<Resultado<LoginResponse>> DesvincularGoogleAsync(
        long idUsuario, DesvincularGoogleRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is null)
        {
            return Resultado<LoginResponse>.Falla(new ErrorServicio(TipoError.NoEncontrado, "No se encontró la cuenta del usuario autenticado."));
        }

        var identidad = await _db.IdentidadesExternas
            .SingleOrDefaultAsync(i => i.IdUsuario == idUsuario && i.Proveedor == ProveedoresIdentidad.Google, cancelacion);
        if (identidad is null)
        {
            return Resultado<LoginResponse>.Falla(new ErrorServicio(TipoError.NoEncontrado, "Tu cuenta no tiene una cuenta de Google vinculada."));
        }

        if (usuario.PasswordHash is null)
        {
            return Resultado<LoginResponse>.Falla(new ErrorServicio(TipoError.Conflicto,
                "Tu cuenta solo entra con Google. Crea primero una contraseña con \"Establecer contraseña\" para no quedarte sin acceso."));
        }

        if (VerificarPasswordActual(usuario, solicitud.PasswordActual, ip) is { } errorPassword)
        {
            return Resultado<LoginResponse>.Falla(errorPassword);
        }

        _db.IdentidadesExternas.Remove(identidad);
        usuario.SelloSeguridad = Guid.NewGuid();   // cierra las sesiones abiertas, también las abiertas con Google
        await _db.SaveChangesAsync(cancelacion);   // una sola transacción

        _cola.Encolar(PlantillasCorreo.AvisoGoogleDesvinculado(usuario.Correo, usuario.Nombre));
        _log.LogInformation("Seguridad: google_desvinculado idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
        return Resultado<LoginResponse>.Ok(await EmitirSesionAsync(usuario, cancelacion));
    }

    private async Task<Resultado<LoginResponse>> EntrarConIdentidadAsync(
        IdentidadExterna vinculada, IdentidadGoogle identidad, string ip, CancellationToken cancelacion)
    {
        var usuario = vinculada.Usuario;
        if (!usuario.Activo)
        {
            _log.LogWarning("Seguridad: login_rechazado metodo=google motivo=cuenta_desactivada idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            return Resultado<LoginResponse>.Falla(CuentaDesactivada());
        }

        // El correo de Google es solo una referencia: se actualiza si cambió, sin tocar el correo de la cuenta.
        if (identidad.Correo != vinculada.CorreoProveedor && identidad.Correo.Length <= ReglasValidacion.CorreoMax)
        {
            vinculada.CorreoProveedor = identidad.Correo;
            await _db.SaveChangesAsync(cancelacion);
        }

        _log.LogInformation("Seguridad: login_exitoso metodo=google idUsuario={IdUsuario} rol={Rol} ip={Ip}", usuario.IdUsuario, usuario.Rol, ip);
        return Resultado<LoginResponse>.Ok(await EmitirSesionAsync(usuario, cancelacion));
    }

    /// <summary>Contraseña actual para vincular o desvincular. Usa el mismo bloqueo por cuenta + IP que el login.</summary>
    private ErrorServicio? VerificarPasswordActual(Usuario usuario, string passwordActual, string ip)
    {
        if (Bloqueado(usuario.Correo, ip) is { } bloqueo)
        {
            return bloqueo;
        }

        if (usuario.PasswordHash is null
            || _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, passwordActual) == PasswordVerificationResult.Failed)
        {
            _intentos.RegistrarFallo(usuario.Correo, ip);
            _log.LogWarning("Seguridad: password_actual_incorrecta idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            return new ErrorServicio(TipoError.Validacion, "La contraseña actual no es correcta.", "passwordActual");
        }

        return null;
    }

    /// <summary>Nombre de Google limitado a los caracteres que admite ClimaPlus; si no queda nada, uno genérico.</summary>
    private static string NombreDesdeGoogle(IdentidadGoogle identidad)
    {
        var limpio = new string((identidad.Nombre ?? string.Empty)
            .Select(c => char.IsLetter(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark
                         || c is ' ' or '.' or '\'' or '-' ? c : ' ')
            .ToArray());
        limpio = Normalizacion.Nombre(limpio).TrimStart('.', '\'', '-', ' ');
        if (limpio.Length > ReglasValidacion.NombreMax)
        {
            limpio = limpio[..ReglasValidacion.NombreMax].TrimEnd();
        }

        return limpio.Length > 0 && char.IsLetter(limpio[0]) ? limpio : "Usuario de Google";
    }

    private ErrorServicio? GoogleNoConfigurado() =>
        _opcionesGoogle.Value.Configurado
            ? null
            : new ErrorServicio(TipoError.ServicioNoDisponible, "El inicio de sesión con Google no está disponible en este momento.");

    private static ErrorServicio GoogleInvalido() =>
        new(TipoError.CredencialesInvalidas, "No se pudo validar el inicio de sesión con Google. Vuelve a intentarlo.");

    private static ErrorServicio CorreoDeCuentaExistente() =>
        new(TipoError.Conflicto,
            "Ya existe una cuenta de ClimaPlus con el correo de esta cuenta de Google. Inicia sesión con tu contraseña y vincula Google desde tu perfil.");

    private static ErrorServicio SubDeOtraCuenta() =>
        new(TipoError.Conflicto, "Esta cuenta de Google ya está vinculada a otra cuenta de ClimaPlus.");
}
