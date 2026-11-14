using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Admin;
using ClimaPlus.Api.DTOs.Comun;
using ClimaPlus.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClimaPlus.Api.Services.Admin;

/// <summary>
/// Gestión de usuarios del administrador (M14): RF-26 listar y filtrar, RF-27 cambiar rol, RF-28 activar/desactivar.
///
/// Reglas:
/// - RN-BD-09: siempre queda al menos un administrador activo; no se degrada ni se desactiva al último.
/// - Un administrador no puede quitarse su propio rol ni desactivar su propia cuenta desde aquí.
/// - RN-BD-08: no hay borrado físico.
/// - Cada cambio renueva usuario.sello_seguridad: todas las sesiones abiertas de esa cuenta dejan de valer (además del
///   rechazo por "rol_cambiado" o "cuenta_desactivada" que ya hace la validación del JWT).
/// - Concurrencia: los cambios de rol y estado se serializan con un bloqueo de transacción de PostgreSQL
///   (pg_advisory_xact_lock), así dos administradores no pueden dejar el sistema sin administradores a la vez.
/// - Auditoría: eventos "admin_rol_cambiado", "admin_estado_cambiado" y sus rechazos en la categoría
///   ClimaPlus.Seguridad, con identificadores y sin correos completos ni secretos.
/// </summary>
public sealed class UsuarioAdminService
{
    /// <summary>
    /// Clave del bloqueo de transacción para los cambios de rol y estado. Se usa la forma de dos enteros
    /// (pg_advisory_xact_lock(int, int)), que no comparte espacio con los bloqueos de una sola clave bigint por usuario
    /// del motor de alertas.
    /// </summary>
    public const int ClaveBloqueoModulo = 14;      // M14
    public const int ClaveBloqueoAdministradores = 26;   // RF-26 a RF-28

    private readonly ClimaPlusDbContext _db;
    private readonly ILogger _log;

    public UsuarioAdminService(ClimaPlusDbContext db, ILoggerFactory logs)
    {
        _db = db;
        _log = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    // ------------------------------------------------------------------ RF-26

    public async Task<Resultado<PaginaResponse<UsuarioAdminResponse>>> ListarAsync(FiltroUsuariosAdminRequest filtro, CancellationToken cancelacion)
    {
        var consulta = _db.Usuarios.AsNoTracking();

        var texto = filtro.Busqueda?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(texto))
        {
            // El correo se guarda en minúsculas; el nombre se compara en minúsculas.
            consulta = consulta.Where(u => u.Correo.Contains(texto) || u.Nombre.ToLower().Contains(texto));
        }

        if (filtro.Rol is { } rol)
        {
            consulta = consulta.Where(u => u.Rol == rol);
        }

        if (filtro.Estado is { } estado)
        {
            var activo = estado == FiltrosUsuarioAdmin.EstadoActivo;
            consulta = consulta.Where(u => u.Activo == activo);
        }

        var total = await consulta.CountAsync(cancelacion);
        var elementos = await Proyectar(consulta
                .OrderByDescending(u => u.FechaCreacion)
                .ThenByDescending(u => u.IdUsuario)
                .Skip((filtro.Pagina - 1) * Paginacion.TamanoPagina)
                .Take(Paginacion.TamanoPagina))
            .ToListAsync(cancelacion);

        return Resultado<PaginaResponse<UsuarioAdminResponse>>.Ok(PaginaResponse<UsuarioAdminResponse>.Crear(elementos, filtro.Pagina, total));
    }

    public async Task<Resultado<UsuarioAdminResponse>> ObtenerAsync(long idUsuario, CancellationToken cancelacion)
    {
        var usuario = await Proyectar(_db.Usuarios.AsNoTracking().Where(u => u.IdUsuario == idUsuario)).SingleOrDefaultAsync(cancelacion);
        return usuario is null
            ? Resultado<UsuarioAdminResponse>.Falla(UsuarioNoEncontrado())
            : Resultado<UsuarioAdminResponse>.Ok(usuario);
    }

    // ------------------------------------------------------------------ RF-27

    public async Task<Resultado<UsuarioAdminResponse>> CambiarRolAsync(
        long idAdministrador, long idUsuario, string rolNuevo, string ip, CancellationToken cancelacion)
    {
        if (idUsuario == idAdministrador && rolNuevo != Roles.Administrador)
        {
            return Rechazar("admin_rol_rechazado", "propio", idAdministrador, idUsuario, ip,
                new ErrorServicio(TipoError.Conflicto, "No puedes quitarte tu propio rol de administrador. Pide a otro administrador que lo haga.", "rol"));
        }

        return await EnTransaccionAsync(async () =>
        {
            var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
            if (usuario is null)
            {
                return Resultado<UsuarioAdminResponse>.Falla(UsuarioNoEncontrado());
            }

            if (usuario.Rol == rolNuevo)
            {
                return Resultado<UsuarioAdminResponse>.Ok(await ResponderAsync(usuario, cancelacion));   // sin cambios
            }

            if (usuario.Rol == Roles.Administrador && usuario.Activo && await AdministradoresActivosAsync(cancelacion) <= 1)
            {
                return Rechazar("admin_rol_rechazado", "ultimo_administrador", idAdministrador, idUsuario, ip,
                    new ErrorServicio(TipoError.Conflicto, "Es el último administrador activo: no se puede degradar (RN-BD-09).", "rol"));
            }

            var anterior = usuario.Rol;
            usuario.Rol = rolNuevo;
            usuario.SelloSeguridad = Guid.NewGuid();   // cierra todas las sesiones de esa cuenta
            await _db.SaveChangesAsync(cancelacion);

            _log.LogInformation("Seguridad: admin_rol_cambiado idAdmin={IdAdmin} idUsuario={IdUsuario} anterior={Anterior} nuevo={Nuevo} ip={Ip}",
                idAdministrador, idUsuario, anterior, rolNuevo, ip);
            return Resultado<UsuarioAdminResponse>.Ok(await ResponderAsync(usuario, cancelacion));
        }, cancelacion);
    }

    // ------------------------------------------------------------------ RF-28

    public async Task<Resultado<UsuarioAdminResponse>> CambiarEstadoAsync(
        long idAdministrador, long idUsuario, bool activo, string ip, CancellationToken cancelacion)
    {
        if (idUsuario == idAdministrador && !activo)
        {
            return Rechazar("admin_estado_rechazado", "propio", idAdministrador, idUsuario, ip,
                new ErrorServicio(TipoError.Conflicto, "No puedes desactivar tu propia cuenta. Pide a otro administrador que lo haga.", "activo"));
        }

        return await EnTransaccionAsync(async () =>
        {
            var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
            if (usuario is null)
            {
                return Resultado<UsuarioAdminResponse>.Falla(UsuarioNoEncontrado());
            }

            if (usuario.Activo == activo)
            {
                return Resultado<UsuarioAdminResponse>.Ok(await ResponderAsync(usuario, cancelacion));   // sin cambios
            }

            if (!activo && usuario.Rol == Roles.Administrador && await AdministradoresActivosAsync(cancelacion) <= 1)
            {
                return Rechazar("admin_estado_rechazado", "ultimo_administrador", idAdministrador, idUsuario, ip,
                    new ErrorServicio(TipoError.Conflicto, "Es el último administrador activo: no se puede desactivar (RN-BD-09).", "activo"));
            }

            usuario.Activo = activo;
            usuario.SelloSeguridad = Guid.NewGuid();   // cierra todas las sesiones de esa cuenta
            await _db.SaveChangesAsync(cancelacion);

            _log.LogInformation("Seguridad: admin_estado_cambiado idAdmin={IdAdmin} idUsuario={IdUsuario} activo={Activo} ip={Ip}",
                idAdministrador, idUsuario, activo, ip);
            return Resultado<UsuarioAdminResponse>.Ok(await ResponderAsync(usuario, cancelacion));
        }, cancelacion);
    }

    // ------------------------------------------------------------------ Apoyo

    /// <summary>
    /// Ejecuta el cambio en una transacción con el bloqueo de administradores (PostgreSQL). InMemory (pruebas) no tiene
    /// transacciones ni bloqueos; la concurrencia real se prueba con PostgreSQL.
    /// </summary>
    private async Task<Resultado<UsuarioAdminResponse>> EnTransaccionAsync(
        Func<Task<Resultado<UsuarioAdminResponse>>> operacion, CancellationToken cancelacion)
    {
        if (!_db.Database.IsRelational())
        {
            return await operacion();
        }

        await using IDbContextTransaction transaccion = await _db.Database.BeginTransactionAsync(cancelacion);
        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({ClaveBloqueoModulo}, {ClaveBloqueoAdministradores})", cancelacion);
        }

        var resultado = await operacion();
        if (resultado.Exitoso)
        {
            await transaccion.CommitAsync(cancelacion);
        }

        return resultado;   // sin éxito: el "await using" revierte la transacción
    }

    private Task<int> AdministradoresActivosAsync(CancellationToken cancelacion) =>
        _db.Usuarios.CountAsync(u => u.Rol == Roles.Administrador && u.Activo, cancelacion);

    private async Task<UsuarioAdminResponse> ResponderAsync(Usuario usuario, CancellationToken cancelacion) => new(
        usuario.IdUsuario,
        usuario.Nombre,
        usuario.Correo,
        usuario.Rol,
        usuario.Activo,
        usuario.CorreoVerificado,
        usuario.PasswordHash is not null,
        await _db.IdentidadesExternas.AnyAsync(i => i.IdUsuario == usuario.IdUsuario, cancelacion),
        DateTime.SpecifyKind(usuario.FechaCreacion, DateTimeKind.Utc));

    private static IQueryable<UsuarioAdminResponse> Proyectar(IQueryable<Usuario> usuarios) =>
        usuarios.Select(u => new UsuarioAdminResponse(
            u.IdUsuario,
            u.Nombre,
            u.Correo,
            u.Rol,
            u.Activo,
            u.CorreoVerificado,
            u.PasswordHash != null,
            u.IdentidadesExternas.Any(),
            DateTime.SpecifyKind(u.FechaCreacion, DateTimeKind.Utc)));

    private Resultado<UsuarioAdminResponse> Rechazar(
        string evento, string motivo, long idAdministrador, long idUsuario, string ip, ErrorServicio error)
    {
        _log.LogWarning("Seguridad: {Evento} motivo={Motivo} idAdmin={IdAdmin} idUsuario={IdUsuario} ip={Ip}",
            evento, motivo, idAdministrador, idUsuario, ip);
        return Resultado<UsuarioAdminResponse>.Falla(error);
    }

    private static ErrorServicio UsuarioNoEncontrado() => new(TipoError.NoEncontrado, "El usuario no existe.");
}
