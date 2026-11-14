namespace ClimaPlus.Api.Correo;

public static class CorreoServiceCollectionExtensions
{
    /// <summary>
    /// Registra el envío de correos: opciones "Correo" (validadas al arrancar), MailKit, la cola y el servicio en
    /// segundo plano. Sin Correo:Host la API arranca (desarrollo sin SMTP) pero no puede enviar códigos; en
    /// Production la configuración es obligatoria.
    /// </summary>
    public static IServiceCollection AddCorreo(this IServiceCollection services, IConfiguration configuracion)
    {
        services.AddOptions<CorreoOptions>()
            .Bind(configuracion.GetSection(CorreoOptions.Seccion))
            .Validate(o => !o.Validar().Any(), "La sección 'Correo' no es válida: revisa Host, Puerto, Remitente y Seguridad (sin TLS solo Mailpit o localhost).")
            .Validate<IHostEnvironment>((o, entorno) => o.Configurado || !entorno.IsProduction(),
                "En Production es obligatorio configurar el correo (SMTP_HOST y SMTP_FROM): sin él nadie puede verificar su cuenta.")
            .ValidateOnStart();

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<ColaCorreos>();
        services.AddSingleton<IColaCorreos>(sp => sp.GetRequiredService<ColaCorreos>());
        services.AddSingleton<ServicioEnvioCorreos>();
        services.AddHostedService(sp => sp.GetRequiredService<ServicioEnvioCorreos>());
        return services;
    }
}
