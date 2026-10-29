using ClimaPlus.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Security;

/// <summary>Dispositivo identificado por su API Key, disponible para la acción en <see cref="HttpContext.Items"/>.</summary>
public sealed record DispositivoAutenticado(long IdDispositivo, long IdUsuario, long IdUbicacion, string NombreUbicacion);

/// <summary>
/// Autentica al ESP32 por la cabecera X-Device-Key (03-arquitectura.md: "validación de API Key → LecturasController").
/// Es un filtro de autorización: se ejecuta ANTES de leer y validar el cuerpo, así una petición sin clave
/// siempre recibe 401 y nunca revela qué campos espera la API.
/// </summary>
public sealed class AutenticarDispositivoAttribute : TypeFilterAttribute
{
    public AutenticarDispositivoAttribute()
        : base(typeof(AutenticacionDispositivoFilter))
    {
    }
}

public sealed class AutenticacionDispositivoFilter : IAsyncAuthorizationFilter
{
    public const string ClaveItems = "ClimaPlus.DispositivoAutenticado";

    private const string ContenidoProblema = "application/problem+json";

    private readonly ClimaPlusDbContext _db;
    private readonly ILogger _seguridad;

    public AutenticacionDispositivoFilter(ClimaPlusDbContext db, ILoggerFactory logs)
    {
        _db = db;
        _seguridad = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext contexto)
    {
        var http = contexto.HttpContext;
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        var apiKey = http.Request.Headers[ApiKeyDispositivo.Cabecera].ToString();

        if (!ApiKeyDispositivo.FormatoValido(apiKey))
        {
            // Nunca se registra la clave recibida.
            _seguridad.LogWarning("Seguridad: dispositivo_rechazado motivo={Motivo} ip={Ip}",
                string.IsNullOrEmpty(apiKey) ? "sin_api_key" : "formato_invalido", ip);
            contexto.Result = NoAutenticado();
            return;
        }

        var hash = ApiKeyDispositivo.Hash(apiKey);
        var dispositivo = await _db.Dispositivos
            .AsNoTracking()
            .Where(d => d.ApiKeyHash == hash)
            .Select(d => new
            {
                d.IdDispositivo,
                d.IdUsuario,
                d.IdUbicacion,
                NombreUbicacion = d.Ubicacion.Nombre,
                d.Activo,
                UsuarioActivo = d.Usuario.Activo,
            })
            .SingleOrDefaultAsync(http.RequestAborted);

        if (dispositivo is null)
        {
            _seguridad.LogWarning("Seguridad: dispositivo_rechazado motivo=api_key_desconocida ip={Ip}", ip);
            contexto.Result = NoAutenticado();
            return;
        }

        // RN-BD-29 / D-11: un dispositivo inactivo no puede enviar lecturas. Tampoco el de una cuenta desactivada.
        if (!dispositivo.Activo || !dispositivo.UsuarioActivo)
        {
            _seguridad.LogWarning("Seguridad: dispositivo_rechazado motivo={Motivo} idDispositivo={IdDispositivo} ip={Ip}",
                !dispositivo.Activo ? "dispositivo_inactivo" : "cuenta_desactivada", dispositivo.IdDispositivo, ip);
            contexto.Result = Problema(StatusCodes.Status403Forbidden, "Dispositivo desactivado",
                !dispositivo.Activo
                    ? "Este dispositivo está desactivado. Actívalo desde Mis dispositivos para volver a enviar lecturas."
                    : "La cuenta del dueño del dispositivo está desactivada.");
            return;
        }

        http.Items[ClaveItems] = new DispositivoAutenticado(
            dispositivo.IdDispositivo, dispositivo.IdUsuario, dispositivo.IdUbicacion, dispositivo.NombreUbicacion);
    }

    private static ObjectResult NoAutenticado() =>
        Problema(StatusCodes.Status401Unauthorized, "Dispositivo no autenticado",
            $"Falta la cabecera {ApiKeyDispositivo.Cabecera} o la API Key no es válida.");

    private static ObjectResult Problema(int estado, string titulo, string detalle)
    {
        var resultado = new ObjectResult(new ProblemDetails { Status = estado, Title = titulo, Detail = detalle })
        {
            StatusCode = estado,
        };
        resultado.ContentTypes.Add(ContenidoProblema);
        return resultado;
    }
}
