using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClimaPlus.Api.Security;

/// <summary>
/// D-21: en rutas públicas ([AllowAnonymous]) un token inválido, expirado o de un usuario
/// desactivado NO se trata como visitante: responde 401 para que el frontend cierre la sesión.
/// Sin encabezado Authorization la petición sigue como visitante.
/// La respuesta la genera el mismo desafío JWT del resto de la API (ProblemDetails 401).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RechazarTokenInvalidoAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext contexto)
    {
        var http = contexto.HttpContext;
        if (http.Request.Headers.ContainsKey("Authorization") && http.User.Identity?.IsAuthenticated != true)
        {
            contexto.Result = new ChallengeResult();
        }
    }
}
