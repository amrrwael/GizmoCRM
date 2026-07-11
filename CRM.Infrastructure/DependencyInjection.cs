using CRM.Application.Common.Interfaces;
using CRM.Infrastructure.Persistence;
using CRM.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
                      .EnableRetryOnFailure(3)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ITokenService, TokenService>();

        // Encrypts and stores every integration credential (Telegram / Gmail / Twilio)
        // in the database, driven entirely by the Settings -> Integrations UI.
        // NOTE for production: mount "dataprotection-keys" as a persistent volume/disk,
        // otherwise a redeploy will generate a new key and existing encrypted
        // credentials in the database will no longer decrypt (users would just need
        // to re-enter them in Settings — nothing else breaks).
        services.AddDataProtection()
            .SetApplicationName("GizmoCRM")
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(AppContext.BaseDirectory, "dataprotection-keys")));
        services.AddScoped<IIntegrationSettingsService, IntegrationSettingsService>();

        // Telegram: previously ITelegramService / HttpClient were never registered at
        // all, so every call into TelegramController or the webhook threw a DI
        // resolution error. AddHttpClient<TInterface, TImplementation> registers both
        // the typed HttpClient AND the ITelegramService -> TelegramService mapping.
        services.AddHttpClient<ITelegramService, TelegramService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Gmail (Google API) — OAuth + send/receive.
        services.AddHttpClient<IGmailService, GmailService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        // Calls (Twilio Voice) — outbound/inbound calling, browser client tokens.
        services.AddScoped<ICallService, CallService>();

        return services;
    }
}
