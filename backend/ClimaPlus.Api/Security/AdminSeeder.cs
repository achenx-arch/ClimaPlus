using System.Text.RegularExpressions;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs;
using ClimaPlus.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Security;

/// <summary>
/// Crea el administrador inicial (D-06) solo si:
///   1) AdminInicial:Correo y AdminInicial:Password están configurados (User Secrets), y
///   2) todavía no existe ningún usuario con rol ADMINISTRADOR.
/// Nunca modifica cuentas existentes.
/// </summary>
public static class AdminSeeder
{
    public static async Task EjecutarAsync(IServiceProvider servicios, CancellationToken cancelacion = default)
    {
        using var alcance = servicios.CreateScope();
        var sp = alcance.ServiceProvider;
        var configuracion = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger(RegistroSeguridad.Categoria);

        var correo = configuracion["AdminInicial:Correo"];
        var password = configuracion["AdminInicial:Password"];
        var nombre = configuracion["AdminInicial:Nombre"];

        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(password))
        {
            log.LogInformation("Seguridad: seeder_admin_omitido motivo=no_configurado");
            return;
        }

        if (!Regex.IsMatch(correo, ReglasValidacion.PatronCorreo)
            || !Regex.IsMatch(password, ReglasValidacion.PatronPassword)
            || password.Length is < ReglasValidacion.PasswordMin or > ReglasValidacion.PasswordMax)
        {
            log.LogWarning("Seguridad: seeder_admin_omitido motivo=configuracion_invalida");
            return;
        }

        try
        {
            var db = sp.GetRequiredService<ClimaPlusDbContext>();

            if (await db.Usuarios.AnyAsync(u => u.Rol == Roles.Administrador, cancelacion))
            {
                return;
            }

            var correoNormalizado = Normalizacion.Correo(correo);
            if (await db.Usuarios.AnyAsync(u => u.Correo == correoNormalizado, cancelacion))
            {
                log.LogWarning("Seguridad: seeder_admin_omitido motivo=correo_en_uso correo={Correo}",
                    RegistroSeguridad.EnmascararCorreo(correoNormalizado));
                return;
            }

            var admin = new Usuario
            {
                Nombre = Normalizacion.Nombre(string.IsNullOrWhiteSpace(nombre) ? "Administrador" : nombre),
                Correo = correoNormalizado,
                Rol = Roles.Administrador,
                Activo = true,
                FechaCreacion = DateTime.UtcNow,
            };
            admin.PasswordHash = sp.GetRequiredService<IPasswordHasher<Usuario>>().HashPassword(admin, password);

            db.Usuarios.Add(admin);
            await db.SaveChangesAsync(cancelacion);

            log.LogInformation("Seguridad: admin_inicial_creado idUsuario={IdUsuario}", admin.IdUsuario);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Seguridad: seeder_admin_fallido");
        }
    }
}
