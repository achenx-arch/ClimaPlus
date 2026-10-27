namespace ClimaPlus.Api.DTOs;

/// <summary>
/// Reglas de validación de entrada compartidas por los DTOs (Fase 2D).
/// Las longitudes máximas coinciden con las columnas de la tabla usuario.
/// </summary>
public static class ReglasValidacion
{
    public const int NombreMax = 100;          // usuario.nombre VARCHAR(100)
    public const int CorreoMax = 150;          // usuario.correo VARCHAR(150)
    public const int PasswordMin = 8;
    public const int PasswordMax = 128;

    /// <summary>Letras (incluidas tildes y ñ), espacios, punto, apóstrofo y guion. Rechaza &lt; &gt; y símbolos.</summary>
    public const string PatronNombre = @"^\s*[\p{L}\p{M}][\p{L}\p{M} .'\-]*\s*$";

    /// <summary>Correo ASCII simple; garantiza que lower() de PostgreSQL y ToLowerInvariant() coincidan.</summary>
    public const string PatronCorreo = @"^\s*[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}\s*$";

    /// <summary>Al menos una letra y un dígito.</summary>
    public const string PatronPassword = @"^(?=.*\p{L})(?=.*\d).+$";

    public const string MensajeNombre = "El nombre solo puede contener letras, espacios, puntos, apóstrofos y guiones.";
    public const string MensajeCorreo = "Ingresa un correo válido.";
    public const string MensajePassword = "La contraseña debe tener entre 8 y 128 caracteres e incluir al menos una letra y un número.";
}
