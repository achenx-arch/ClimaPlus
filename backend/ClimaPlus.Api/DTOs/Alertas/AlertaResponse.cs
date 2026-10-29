using ClimaPlus.Api.DTOs.Clima;

namespace ClimaPlus.Api.DTOs.Alertas;

/// <summary>Alerta generada (M09). Las alertas son historial: solo cambia el estado (RN-BD-26).</summary>
public sealed record AlertaResponse(
    long IdAlerta,
    string Tipo,
    string Mensaje,
    string Estado,
    decimal? ValorDetectado,
    decimal? ValorUmbral,
    DateTime FechaGeneracion,
    UbicacionResponse Ubicacion,
    OrigenAlertaResponse Origen);

/// <summary>
/// Origen de la alerta (RN-BD-23): una consulta ACTUAL de OpenWeather o una lectura del ESP32.
/// Para ESP32 incluye el dispositivo, para navegar a M12.
/// </summary>
public sealed record OrigenAlertaResponse(
    string Tipo,
    long? IdConsulta,
    long? IdLectura,
    long? IdDispositivo,
    string? NombreDispositivo);

/// <summary>Usuario destinatario, solo en la vista de administración (M15).</summary>
public sealed record UsuarioAlertaResponse(long IdUsuario, string Nombre, string Correo);

/// <summary>Alerta en la vista de administración (M15): añade el usuario destinatario.</summary>
public sealed record AlertaAdminResponse(
    long IdAlerta,
    string Tipo,
    string Mensaje,
    string Estado,
    decimal? ValorDetectado,
    decimal? ValorUmbral,
    DateTime FechaGeneracion,
    UbicacionResponse Ubicacion,
    OrigenAlertaResponse Origen,
    UsuarioAlertaResponse Usuario);

/// <summary>GET /api/alertas/contador: alertas NUEVA del usuario (campana y M06).</summary>
public sealed record ContadorAlertasResponse(int Nuevas);
