using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>Guarda todos los mensajes de log de la API para comprobar que no filtran secretos.</summary>
public sealed class LogsCapturados : ILoggerProvider
{
    public ConcurrentQueue<string> Mensajes { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Registrador(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Registrador : ILogger
    {
        private readonly LogsCapturados _destino;
        private readonly string _categoria;

        public Registrador(LogsCapturados destino, string categoria)
        {
            _destino = destino;
            _categoria = categoria;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _destino.Mensajes.Enqueue($"{_categoria}: {formatter(state, exception)} {exception}");
    }
}
