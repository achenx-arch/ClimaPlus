using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ClimaPlus.Api.DTOs.Comun;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.DTOs.Alertas;

/// <summary>Valores permitidos en los filtros de alertas.</summary>
public static class FiltrosAlerta
{
    public const string PatronTipo = "^(LLUVIA|TEMPERATURA_ALTA|TEMPERATURA_BAJA)$";
    public const string PatronEstado = "^(NUEVA|LEIDA)$";
    public const string PatronOrigen = "^(OPENWEATHER|ESP32)$";

    public const string OrigenOpenWeather = "OPENWEATHER";
    public const string OrigenEsp32 = "ESP32";
}

/// <summary>GET /api/alertas (M09): filtros por estado y tipo, y paginación.</summary>
public class FiltroAlertasRequest
{
    [FromQuery(Name = "estado")]
    [RegularExpression(FiltrosAlerta.PatronEstado, ErrorMessage = "El estado debe ser NUEVA o LEIDA.")]
    public string? Estado { get; set; }

    [FromQuery(Name = "tipo")]
    [RegularExpression(FiltrosAlerta.PatronTipo, ErrorMessage = "El tipo debe ser LLUVIA, TEMPERATURA_ALTA o TEMPERATURA_BAJA.")]
    public string? Tipo { get; set; }

    [FromQuery(Name = "pagina")]
    [Range(1, Paginacion.PaginaMaxima, ErrorMessage = "La página debe ser un número mayor o igual que 1.")]
    public int Pagina { get; set; } = 1;
}

/// <summary>GET /api/admin/alertas (M15): además, origen, rango de fechas y usuario.</summary>
public sealed class FiltroAlertasAdminRequest : FiltroAlertasRequest
{
    [FromQuery(Name = "origen")]
    [RegularExpression(FiltrosAlerta.PatronOrigen, ErrorMessage = "El origen debe ser OPENWEATHER o ESP32.")]
    public string? Origen { get; set; }

    /// <summary>Incluido. Fecha y hora ISO 8601 con zona (por ejemplo 2026-10-01T00:00:00-06:00).</summary>
    [FromQuery(Name = "desde")]
    public DateTimeOffset? Desde { get; set; }

    /// <summary>Excluido.</summary>
    [FromQuery(Name = "hasta")]
    public DateTimeOffset? Hasta { get; set; }

    [FromQuery(Name = "idUsuario")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "El usuario no es válido.")]
    public long? IdUsuario { get; set; }
}

/// <summary>PATCH /api/alertas/{id}/estado. Solo se permite marcar como LEIDA (RN-BD-25, RN-BD-26).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CambiarEstadoAlertaRequest
{
    [Required(ErrorMessage = "Indica el estado.")]
    [RegularExpression("^LEIDA$", ErrorMessage = "Una alerta solo se puede marcar como LEIDA.")]
    public string? Estado { get; init; }
}
