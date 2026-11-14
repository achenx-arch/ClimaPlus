using System.Collections.Concurrent;
using ClimaPlus.Api.Correo;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Envuelve la cola real de correos y anota cada mensaje en el momento en que la API lo encola, es decir, antes de
/// responder la petición. Los ayudantes de pruebas leen de aquí el código de un registro o de un inicio de sesión de
/// forma determinista (sin esperar al envío en segundo plano). El envío sigue pasando por la cola real y por
/// <see cref="EnviadorCorreoFalso"/>, que es donde se prueban los envíos, los fallos SMTP y los topes.
/// </summary>
public sealed class ColaCorreosObservada : IColaCorreos
{
    // Compartido por todas las fábricas: los correos de prueba son únicos (Guid).
    private static readonly ConcurrentQueue<MensajeCorreo> Encolados = new();

    private readonly ColaCorreos _cola;

    public ColaCorreosObservada(ColaCorreos cola)
    {
        _cola = cola;
    }

    public bool Encolar(MensajeCorreo mensaje)
    {
        Encolados.Enqueue(mensaje);
        return _cola.Encolar(mensaje);
    }

    /// <summary>Correos con código encolados para ese destinatario, en orden.</summary>
    public static IReadOnlyList<MensajeCorreo> CodigosPara(string destinatario) =>
        Encolados.Where(m => EnviadorCorreoFalso.EsCorreoConCodigo(m)
                             && string.Equals(m.Destinatario, destinatario, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Código del correo número <paramref name="indice"/> (0 = el primero) para ese destinatario. Falla con un mensaje
    /// claro si la API no lo encoló (por ejemplo, por el tope de 5 códigos por hora de la cuenta).
    /// </summary>
    public static string Codigo(string destinatario, int indice)
    {
        var codigos = CodigosPara(destinatario);
        Assert.True(codigos.Count > indice, $"La API no encoló el código número {indice + 1} para el destinatario.");
        return EnviadorCorreoFalso.ExtraerCodigo(codigos[indice]);
    }
}
