using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ClimaPlus.Api.Services.Autenticacion;

/// <summary>Titular de un desafío real (para los bloqueos por cuenta). Nunca sale en una respuesta HTTP.</summary>
public sealed record TitularDesafio(long IdUsuario, string Correo);

/// <summary>Un desafío recién emitido: el código solo existe en memoria hasta encolar el correo.</summary>
public sealed record DesafioEmitido(Guid IdDesafio, string Proposito, string Codigo, DateTime ExpiraEn);

/// <summary>
/// Desafíos de autenticación (tabla desafio_autenticacion): códigos de 6 dígitos de un solo uso, por propósito.
/// - El código se genera con RandomNumberGenerator y solo se guarda su HMAC-SHA256 de "id:propósito:código" con la
///   clave Codigos:ClaveHmac: un código de un propósito nunca sirve para otro y la base robada no basta para probarlos.
/// - Un desafío nuevo anula el anterior vivo del mismo usuario y propósito (índice único parcial).
/// - Consumir es atómico: "estado" e "intentos" son tokens de concurrencia, así que dos peticiones simultáneas con el
///   mismo código no pueden consumirlo dos veces.
/// - Los señuelos (condición 3) no tienen fila: toda búsqueda por su id no encuentra nada y responde igual que un
///   código inválido, con el mismo cálculo de HMAC.
/// </summary>
public sealed class ServicioDesafios
{
    private const int ReintentosConcurrencia = 3;

    private readonly ClimaPlusDbContext _db;
    private readonly IOptions<CodigosOptions> _opciones;
    private readonly TimeProvider _reloj;

    public ServicioDesafios(ClimaPlusDbContext db, IOptions<CodigosOptions> opciones, TimeProvider reloj)
    {
        _db = db;
        _opciones = opciones;
        _reloj = reloj;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public int MinutosVigencia(string proposito) => proposito switch
    {
        PropositosDesafio.InicioSesion or PropositosDesafio.InicioGoogle or PropositosDesafio.VincularGoogle => _opciones.Value.MinutosInicioSesion,
        PropositosDesafio.CambiarPassword => _opciones.Value.MinutosCambioPassword,
        _ => _opciones.Value.MinutosVerificacion,
    };

    /// <summary>Código de 6 dígitos uniformemente aleatorio (000000 a 999999).</summary>
    public static string GenerarCodigo() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>HMAC-SHA256 en hexadecimal (64 caracteres) de "id:propósito:código".</summary>
    public string Hash(Guid idDesafio, string proposito, string codigo)
    {
        var mensaje = string.Create(CultureInfo.InvariantCulture, $"{idDesafio:N}:{proposito}:{codigo}");
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_opciones.Value.ClaveHmac), Encoding.UTF8.GetBytes(mensaje));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    private static bool HashesIguales(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    /// <summary>
    /// Crea un desafío para el usuario (anulando el vivo del mismo propósito) y lo guarda. Si
    /// <paramref name="conCodigo"/> es false (vinculación con Google antes de confirmar la contraseña), queda sin
    /// código hasta <see cref="RegenerarCodigoAsync"/>.
    /// </summary>
    public async Task<DesafioEmitido> CrearAsync(
        Usuario usuario, string proposito, CancellationToken cancelacion, string? sujetoExterno = null, bool conCodigo = true)
    {
        for (var intento = 1; ; intento++)
        {
            var ahora = Ahora;
            var vivos = await _db.DesafiosAutenticacion
                .Where(d => d.IdUsuario == usuario.IdUsuario && d.Proposito == proposito
                         && (d.Estado == EstadosDesafio.Pendiente || d.Estado == EstadosDesafio.Verificado))
                .ToListAsync(cancelacion);
            foreach (var vivo in vivos)
            {
                vivo.Estado = EstadosDesafio.Anulado;
                vivo.FinalizadoEn = ahora;
            }

            var id = Guid.NewGuid();
            var codigo = GenerarCodigo();
            var desafio = new DesafioAutenticacion
            {
                IdDesafio = id,
                IdUsuario = usuario.IdUsuario,
                Proposito = proposito,
                Estado = EstadosDesafio.Pendiente,
                CodigoHash = conCodigo ? Hash(id, proposito, codigo) : null,
                EnviadoEn = conCodigo ? ahora : null,
                ExpiraEn = ahora.AddMinutes(MinutosVigencia(proposito)),
                CreadoEn = ahora,
                SujetoExterno = sujetoExterno,
            };
            _db.DesafiosAutenticacion.Add(desafio);

            try
            {
                await _db.SaveChangesAsync(cancelacion);
                return new DesafioEmitido(id, proposito, codigo, desafio.ExpiraEn);
            }
            catch (DbUpdateException ex) when (intento < ReintentosConcurrencia && EsConflicto(ex))
            {
                // Otra petición creó o anuló un desafío del mismo propósito a la vez: se vuelve a intentar.
                _db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Señuelo: mismo aspecto que un desafío real, sin fila en la base de datos.</summary>
    public DesafioEmitido CrearSenuelo(string proposito) =>
        new(Guid.NewGuid(), proposito, GenerarCodigo(), Ahora.AddMinutes(MinutosVigencia(proposito)));

    /// <summary>
    /// Usuario dueño de un desafío, o null si no existe (señuelo o token de otro propósito). Solo lectura: sirve para
    /// comprobar bloqueos por cuenta antes de validar el código. El usuario sale siempre de la fila del desafío,
    /// nunca del token (condición 3).
    /// </summary>
    public Task<TitularDesafio?> BuscarTitularAsync(Guid idDesafio, string proposito, CancellationToken cancelacion) =>
        _db.DesafiosAutenticacion.AsNoTracking()
            .Where(d => d.IdDesafio == idDesafio && d.Proposito == proposito)
            .Select(d => new TitularDesafio(d.IdUsuario, d.Usuario.Correo))
            .SingleOrDefaultAsync(cancelacion);

    /// <summary>
    /// Valida el código. Si es correcto, el desafío pasa a <paramref name="estadoFinal"/> (CONSUMIDO, o VERIFICADO en
    /// la recuperación) y se ejecuta <paramref name="alAceptar"/> en la misma transacción (por ejemplo, marcar el
    /// correo como verificado). Devuelve el desafío con su usuario, o null ante cualquier fallo: código incorrecto,
    /// vencido, ya usado, agotado o desafío inexistente (señuelo) responden igual. <paramref name="alFallar"/> se
    /// ejecuta cuando un desafío real y vigente recibe un código incorrecto (ya contado), para los bloqueos por cuenta.
    /// Con <paramref name="anularOtrosVivos"/>, al aceptar se anulan también los demás desafíos vivos (PENDIENTE o
    /// VERIFICADO, de cualquier propósito) del mismo usuario, en el mismo SaveChanges: si cualquier parte falla, no se
    /// guarda nada (el código sigue sin usar y la contraseña sin cambiar).
    /// </summary>
    public async Task<DesafioAutenticacion?> VerificarAsync(
        Guid idDesafio, string proposito, string codigo, string estadoFinal, CancellationToken cancelacion,
        Action<DesafioAutenticacion>? alAceptar = null, Action<DesafioAutenticacion>? alFallar = null,
        bool anularOtrosVivos = false)
    {
        var maxIntentos = _opciones.Value.MaxIntentos;
        var recibido = Hash(idDesafio, proposito, codigo);

        for (var intento = 1; intento <= ReintentosConcurrencia; intento++)
        {
            _db.ChangeTracker.Clear();
            var ahora = Ahora;
            var desafio = await _db.DesafiosAutenticacion
                .Include(d => d.Usuario)
                .SingleOrDefaultAsync(d => d.IdDesafio == idDesafio && d.Proposito == proposito, cancelacion);

            // Siempre se compara contra algo, exista o no el desafío: el tiempo de respuesta no lo delata.
            var coincide = HashesIguales(desafio?.CodigoHash ?? new string('0', 64), recibido);

            if (desafio is null
                || desafio.CodigoHash is null
                || desafio.Estado != EstadosDesafio.Pendiente
                || desafio.ExpiraEn <= ahora
                || desafio.Intentos >= maxIntentos)
            {
                return null;
            }

            if (!coincide)
            {
                desafio.Intentos++;
                if (desafio.Intentos >= maxIntentos)
                {
                    desafio.Estado = EstadosDesafio.Anulado;
                    desafio.FinalizadoEn = ahora;
                }

                if (await GuardarAsync(cancelacion))
                {
                    alFallar?.Invoke(desafio);
                    return null;
                }

                continue;   // otro intento se registró a la vez: se recarga y se cuenta este también
            }

            desafio.Estado = estadoFinal;
            if (estadoFinal == EstadosDesafio.Verificado)
            {
                desafio.VerificadoEn = ahora;
            }
            else
            {
                desafio.FinalizadoEn = ahora;
            }

            alAceptar?.Invoke(desafio);

            if (anularOtrosVivos)
            {
                var otros = await _db.DesafiosAutenticacion
                    .Where(d => d.IdUsuario == desafio.IdUsuario && d.IdDesafio != desafio.IdDesafio
                             && (d.Estado == EstadosDesafio.Pendiente || d.Estado == EstadosDesafio.Verificado))
                    .ToListAsync(cancelacion);
                foreach (var otro in otros)
                {
                    otro.Estado = EstadosDesafio.Anulado;
                    otro.FinalizadoEn = ahora;
                }
            }

            // Si otra petición lo consumió (o falló el último intento) a la vez, este código ya no vale.
            return await GuardarAsync(cancelacion) ? desafio : null;
        }

        return null;
    }

    /// <summary>
    /// Restablecer la contraseña: pasa a CONSUMIDO un desafío RECUPERAR_PASSWORD que esté VERIFICADO desde hace menos
    /// de <paramref name="ventana"/>, y en el mismo SaveChanges (una sola transacción) aplica
    /// <paramref name="alConsumir"/> sobre el usuario. El estado es token de concurrencia: si dos peticiones usan el
    /// mismo permiso a la vez, solo una gana. Devuelve null si no existe (señuelo), no está verificado, ya se usó, se
    /// anuló o venció la ventana.
    /// </summary>
    public async Task<DesafioAutenticacion?> ConsumirVerificadoAsync(
        Guid idDesafio, TimeSpan ventana, Action<DesafioAutenticacion> alConsumir, CancellationToken cancelacion)
    {
        _db.ChangeTracker.Clear();
        var ahora = Ahora;
        var desafio = await _db.DesafiosAutenticacion
            .Include(d => d.Usuario)
            .SingleOrDefaultAsync(d => d.IdDesafio == idDesafio && d.Proposito == PropositosDesafio.RecuperarPassword, cancelacion);
        if (desafio is null
            || desafio.Estado != EstadosDesafio.Verificado
            || desafio.VerificadoEn is not { } verificado
            || ahora - verificado >= ventana)
        {
            return null;
        }

        desafio.Estado = EstadosDesafio.Consumido;
        desafio.FinalizadoEn = ahora;
        alConsumir(desafio);
        return await GuardarAsync(cancelacion) ? desafio : null;
    }

    /// <summary>
    /// Genera un código nuevo para un desafío PENDIENTE y vigente (invalida el anterior y renueva la vigencia).
    /// Devuelve null si no existe (señuelo), ya se usó, venció o se agotaron sus intentos.
    /// </summary>
    public async Task<(DesafioAutenticacion Desafio, DesafioEmitido Emitido)?> RegenerarCodigoAsync(
        Guid idDesafio, string proposito, CancellationToken cancelacion)
    {
        var ahora = Ahora;
        var desafio = await _db.DesafiosAutenticacion
            .Include(d => d.Usuario)
            .SingleOrDefaultAsync(d => d.IdDesafio == idDesafio && d.Proposito == proposito, cancelacion);
        if (desafio is null
            || desafio.Estado != EstadosDesafio.Pendiente
            || desafio.ExpiraEn <= ahora
            || desafio.Intentos >= _opciones.Value.MaxIntentos)
        {
            return null;
        }

        var codigo = GenerarCodigo();
        if (desafio.CodigoHash is not null && desafio.Reenvios < _opciones.Value.MaxReenvios)
        {
            desafio.Reenvios++;
        }

        desafio.CodigoHash = Hash(idDesafio, proposito, codigo);
        desafio.EnviadoEn = ahora;
        desafio.ExpiraEn = ahora.AddMinutes(MinutosVigencia(proposito));
        if (!await GuardarAsync(cancelacion))
        {
            return null;
        }

        return (desafio, new DesafioEmitido(idDesafio, proposito, codigo, desafio.ExpiraEn));
    }

    private async Task<bool> GuardarAsync(CancellationToken cancelacion)
    {
        try
        {
            await _db.SaveChangesAsync(cancelacion);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static bool EsConflicto(DbUpdateException ex) =>
        ex is DbUpdateConcurrencyException
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
