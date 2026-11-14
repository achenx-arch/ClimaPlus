using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Security;

/// <summary>Para qué se pidió el nonce: no se puede usar uno de login para vincular, ni al revés.</summary>
public enum UsoNonceGoogle
{
    Login,
    Vincular,
}

/// <summary>
/// Nonces de un solo uso para Google Identity Services (OIDC "nonce"). La API emite un valor aleatorio de 256 bits,
/// el navegador se lo pasa a Google y Google lo firma dentro del ID token. Al recibir el token, la API exige que el
/// nonce exista, no haya vencido, sea del mismo uso (y del mismo usuario al vincular) y lo borra: un ID token robado
/// o capturado no se puede reutilizar (replay). En memoria: suficiente para una sola instancia de la API.
/// </summary>
public sealed class NoncesGoogle
{
    private const int EntradasMaximas = 50_000;

    private readonly ConcurrentDictionary<string, Registro> _nonces = new(StringComparer.Ordinal);
    private readonly IOptions<GoogleOptions> _opciones;
    private readonly TimeProvider _reloj;

    public NoncesGoogle(IOptions<GoogleOptions> opciones, TimeProvider reloj)
    {
        _opciones = opciones;
        _reloj = reloj;
    }

    /// <summary>Emite un nonce nuevo, o null si el almacén está lleno (el endpoint responde 429).</summary>
    public (string Nonce, DateTime ExpiraEn)? Emitir(UsoNonceGoogle uso, long? idUsuario)
    {
        var ahora = _reloj.GetUtcNow();
        if (_nonces.Count >= EntradasMaximas)
        {
            Depurar(ahora);
            if (_nonces.Count >= EntradasMaximas)
            {
                return null;
            }
        }

        var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var expira = ahora.AddMinutes(_opciones.Value.MinutosNonce);
        _nonces[nonce] = new Registro(uso, idUsuario, expira);
        return (nonce, expira.UtcDateTime);
    }

    /// <summary>true si el nonce existía, estaba vigente y era de ese uso y usuario. Siempre lo borra (un solo uso).</summary>
    public bool Consumir(string? nonce, UsoNonceGoogle uso, long? idUsuario)
    {
        if (string.IsNullOrEmpty(nonce) || !_nonces.TryRemove(nonce, out var registro))
        {
            return false;
        }

        return registro.Uso == uso && registro.IdUsuario == idUsuario && registro.ExpiraEn > _reloj.GetUtcNow();
    }

    private void Depurar(DateTimeOffset ahora)
    {
        foreach (var (nonce, registro) in _nonces)
        {
            if (registro.ExpiraEn <= ahora)
            {
                _nonces.TryRemove(nonce, out _);
            }
        }
    }

    private sealed record Registro(UsoNonceGoogle Uso, long? IdUsuario, DateTimeOffset ExpiraEn);
}
