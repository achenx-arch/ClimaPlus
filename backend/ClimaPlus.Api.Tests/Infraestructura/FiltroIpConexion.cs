using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Solo pruebas: la cabecera <see cref="Cabecera"/> fija la IP de quien abre la conexión TCP con la API, para simular
/// clientes en IP distintas (proxy inverso, bloqueos por cuenta + IP). Se ejecuta antes que todo el pipeline.
/// </summary>
public sealed class FiltroIpConexion : IStartupFilter
{
    public const string Cabecera = "X-Prueba-Ip-Conexion";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
    {
        app.Use((contexto, continuar) =>
        {
            if (contexto.Request.Headers.TryGetValue(Cabecera, out var ip))
            {
                contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
            }

            return continuar(contexto);
        });
        siguiente(app);
    };
}
