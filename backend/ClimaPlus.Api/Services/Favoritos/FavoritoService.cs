using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Clima;
using ClimaPlus.Api.DTOs.Favoritos;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Favoritos;

/// <summary>
/// Favoritos del usuario autenticado (Fase 2F, M07). Todas las consultas filtran por el
/// id del token: un favorito ajeno se trata igual que uno inexistente (404), para no
/// revelar que existe (07-seguridad-inicial.md).
/// </summary>
public sealed class FavoritoService
{
    private readonly ClimaPlusDbContext _db;
    private readonly ILogger<FavoritoService> _log;

    public FavoritoService(ClimaPlusDbContext db, ILogger<FavoritoService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<Resultado<IReadOnlyList<FavoritoResponse>>> ListarAsync(long idUsuario, CancellationToken cancelacion)
    {
        var favoritos = await Proyectar(DelUsuario(idUsuario)
                .OrderByDescending(f => f.FechaCreacion)
                .ThenByDescending(f => f.IdFavorito), idUsuario)
            .ToListAsync(cancelacion);

        return Resultado<IReadOnlyList<FavoritoResponse>>.Ok(favoritos);
    }

    public async Task<Resultado<FavoritoResponse>> ObtenerAsync(long idUsuario, long idFavorito, CancellationToken cancelacion)
    {
        var favorito = await Proyectar(DelUsuario(idUsuario).Where(f => f.IdFavorito == idFavorito), idUsuario)
            .SingleOrDefaultAsync(cancelacion);
        return favorito is null
            ? Resultado<FavoritoResponse>.Falla(NoEncontrado())
            : Resultado<FavoritoResponse>.Ok(favorito);
    }

    public async Task<Resultado<FavoritoResponse>> AgregarAsync(long idUsuario, CrearFavoritoRequest solicitud, CancellationToken cancelacion)
    {
        var idUbicacion = solicitud.IdUbicacion!.Value;

        if (!await _db.Ubicaciones.AnyAsync(u => u.IdUbicacion == idUbicacion, cancelacion))
        {
            return Resultado<FavoritoResponse>.Falla(new ErrorServicio(TipoError.NoEncontrado,
                "La ubicación no existe. Búscala de nuevo para agregarla.", "idUbicacion"));
        }

        // RN-BD-02: validación previa para dar un mensaje claro; la BD lo garantiza con uq_favorito_usuario_ubicacion.
        if (await _db.Favoritos.AnyAsync(f => f.IdUsuario == idUsuario && f.IdUbicacion == idUbicacion, cancelacion))
        {
            return Resultado<FavoritoResponse>.Falla(Duplicado());
        }

        var favorito = new Favorito
        {
            IdUsuario = idUsuario,
            IdUbicacion = idUbicacion,
            FechaCreacion = DateTime.UtcNow,
        };
        _db.Favoritos.Add(favorito);

        try
        {
            await _db.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException ex) when (AuthService.EsViolacionDeUnicidad(ex))
        {
            // Dos peticiones simultáneas con la misma ubicación.
            _db.Entry(favorito).State = EntityState.Detached;
            return Resultado<FavoritoResponse>.Falla(Duplicado());
        }

        _log.LogInformation("Favoritos: favorito_agregado idUsuario={IdUsuario} idFavorito={IdFavorito} idUbicacion={IdUbicacion}",
            idUsuario, favorito.IdFavorito, idUbicacion);

        // Se responde con el registro leído de la base de datos.
        return await ObtenerAsync(idUsuario, favorito.IdFavorito, cancelacion);
    }

    public async Task<Resultado> EliminarAsync(long idUsuario, long idFavorito, CancellationToken cancelacion)
    {
        var favorito = await _db.Favoritos
            .SingleOrDefaultAsync(f => f.IdFavorito == idFavorito && f.IdUsuario == idUsuario, cancelacion);

        if (favorito is null)
        {
            return Resultado.Falla(NoEncontrado());
        }

        // Solo se borra la relación; la ubicación es un catálogo compartido y se conserva.
        _db.Favoritos.Remove(favorito);
        await _db.SaveChangesAsync(cancelacion);

        _log.LogInformation("Favoritos: favorito_eliminado idUsuario={IdUsuario} idFavorito={IdFavorito}", idUsuario, idFavorito);
        return Resultado.Ok();
    }

    /// <summary>Punto único de aislamiento: solo favoritos cuyo dueño es el usuario del token.</summary>
    private IQueryable<Favorito> DelUsuario(long idUsuario) =>
        _db.Favoritos.AsNoTracking().Where(f => f.IdUsuario == idUsuario);

    /// <summary>
    /// Proyección final (después de filtrar y ordenar, para que EF Core lo traduzca a SQL).
    /// La última consulta solo se busca entre las del mismo usuario.
    /// </summary>
    private IQueryable<FavoritoResponse> Proyectar(IQueryable<Favorito> favoritos, long idUsuario) =>
        favoritos
            .Select(f => new FavoritoResponse(
                f.IdFavorito,
                f.FechaCreacion,
                new UbicacionResponse(
                    f.Ubicacion.IdUbicacion,
                    f.Ubicacion.Nombre,
                    f.Ubicacion.Region,
                    f.Ubicacion.Pais,
                    f.Ubicacion.Latitud,
                    f.Ubicacion.Longitud),
                _db.ConsultasClima
                    .Where(c => c.IdUsuario == idUsuario
                             && c.IdUbicacion == f.IdUbicacion
                             && c.TipoConsulta == TiposConsulta.Actual)
                    .OrderByDescending(c => c.FechaConsulta)
                    .ThenByDescending(c => c.IdConsulta)
                    .Select(c => new UltimaConsultaResponse
                    {
                        IdConsulta = c.IdConsulta,
                        FechaConsulta = c.FechaConsulta,
                        Temperatura = c.Temperatura,
                        CodigoCondicion = c.CodigoCondicion,
                        Descripcion = c.DescripcionCondicion,
                    })
                    .FirstOrDefault()));

    private static ErrorServicio NoEncontrado() =>
        new(TipoError.NoEncontrado, "El favorito no existe.");

    private static ErrorServicio Duplicado() =>
        new(TipoError.Conflicto, "Esta ubicación ya está en tus favoritos.", "idUbicacion");
}
