using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Sección "ProxyInverso" (hallazgo H-05 de la auditoría). Detrás de Caddy o Nginx, la API ve la IP del proxy:
/// el rate limiting y el bloqueo por IP se compartirían entre todos los usuarios y no se enviaría HSTS. Con las
/// redes del proxy declaradas, la API toma la IP real del cliente de X-Forwarded-For y el esquema de
/// X-Forwarded-Proto, pero SOLO cuando la petición llega desde esas redes (nadie más puede falsificar su IP).
/// Vacío (desarrollo, Docker local sin proxy) = no se leen esas cabeceras.
/// </summary>
public sealed class ProxyInversoOptions
{
    public const string Seccion = "ProxyInverso";

    /// <summary>Redes CIDR desde las que llega el proxy, por ejemplo "172.16.0.0/12" (redes de Docker).</summary>
    public List<string> RedesConocidas { get; set; } = new();

    /// <summary>IP exactas del proxy, por ejemplo "10.0.0.5".</summary>
    public List<string> ProxiesConocidos { get; set; } = new();
}

public static class ProxyInverso
{
    /// <summary>
    /// Opciones de ForwardedHeaders a partir de la configuración, o null si no hay proxy declarado.
    /// Lanza InvalidOperationException con un mensaje claro si una red o IP no es válida.
    /// </summary>
    public static ForwardedHeadersOptions? CrearOpciones(ProxyInversoOptions configuracion)
    {
        var redes = configuracion.RedesConocidas.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList();
        var proxies = configuracion.ProxiesConocidos.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
        if (redes.Count == 0 && proxies.Count == 0)
        {
            return null;
        }

        var opciones = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            // Un solo proxy delante: solo se confía en la última entrada que agregó ese proxy.
            ForwardLimit = 1,
        };
        opciones.KnownNetworks.Clear();
        opciones.KnownProxies.Clear();

        foreach (var red in redes)
        {
            opciones.KnownNetworks.Add(LeerRed(red));
        }

        foreach (var proxy in proxies)
        {
            if (!IPAddress.TryParse(proxy, out var ip))
            {
                throw new InvalidOperationException($"ProxyInverso:ProxiesConocidos contiene una IP no válida: '{proxy}'.");
            }

            opciones.KnownProxies.Add(ip);
        }

        return opciones;
    }

    private static Microsoft.AspNetCore.HttpOverrides.IPNetwork LeerRed(string texto)
    {
        var partes = texto.Split('/');
        if (partes.Length == 2
            && IPAddress.TryParse(partes[0], out var prefijo)
            && int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out var longitud)
            && longitud >= 0
            && longitud <= (prefijo.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32))
        {
            return new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefijo, longitud);
        }

        throw new InvalidOperationException(
            $"ProxyInverso:RedesConocidas contiene una red no válida: '{texto}'. Usa notación CIDR, por ejemplo 172.16.0.0/12.");
    }
}
