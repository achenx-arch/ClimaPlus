namespace ClimaPlus.Api.Services;

/// <summary>Normalización de datos de usuario antes de guardarlos o compararlos.</summary>
public static class Normalizacion
{
    /// <summary>RN-BD-04: el correo se guarda sin espacios y en minúsculas.</summary>
    public static string Correo(string correo) => correo.Trim().ToLowerInvariant();

    /// <summary>Quita espacios al inicio y al final y reduce los espacios repetidos a uno.</summary>
    public static string Nombre(string nombre) =>
        string.Join(' ', nombre.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
