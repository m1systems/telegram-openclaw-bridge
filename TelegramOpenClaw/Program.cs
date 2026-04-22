using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramOpenClaw.Settings;

namespace TelegramOpenClaw
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var externalConfigDir = Path.Combine(home, ".config", "telegram-openclaw");
            var externalConfigPath = Path.Combine(externalConfigDir, "appsettings.json");

            var builder = Host.CreateApplicationBuilder(args);

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            builder.Configuration.Sources.Clear();
            builder.Configuration
                .AddJsonFile(externalConfigPath, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            builder.Services
                .AddOptions<TelegramSettings>()
                .Bind(builder.Configuration.GetSection("Telegram"))
                .Validate(
                    settings => !string.IsNullOrWhiteSpace(settings.BotToken),
                    "Telegram:BotToken is required.")
                .Validate(
                    settings => settings.AllowedUserIds != null && settings.AllowedUserIds.Count > 0,
                    "Telegram:AllowedUserIds must contain at least one user ID.")
                .Validate(
                    settings => settings.Polling != null,
                    "Telegram:Polling section is required.")
                .Validate(
                    settings => settings.Polling.TimeoutSeconds > 0,
                    "Telegram:Polling:TimeoutSeconds must be greater than 0.")
                .Validate(
                    settings => settings.Polling.IdleDelayMilliseconds >= 0,
                    "Telegram:Polling:IdleDelayMilliseconds must be 0 or greater.")
                .Validate(
                    settings => settings.RateLimit != null,
                    "Telegram:RateLimit section is required.")
                .Validate(
                    settings => settings.RateLimit.WindowSeconds > 0,
                    "Telegram:RateLimit:WindowSeconds must be greater than 0.")
                .Validate(
                    settings => settings.RateLimit.MaxCommandsPerWindow > 0,
                    "Telegram:RateLimit:MaxCommandsPerWindow must be greater than 0.")
                .Validate(
                    settings => settings.RateLimit.ViolationMuteMinutes >= 0,
                    "Telegram:RateLimit:ViolationMuteMinutes must be 0 or greater.")
                .Validate(
                    settings => settings.Unauthorized != null,
                    "Telegram:Unauthorized section is required.")
                .Validate(
                    settings => !string.IsNullOrWhiteSpace(settings.Unauthorized.ResponseText),
                    "Telegram:Unauthorized:ResponseText is required.")
                .Validate(
                    settings => !string.IsNullOrWhiteSpace(settings.RateLimit.ViolationResponseText),
                    "Telegram:RateLimit:ViolationResponseText is required.")
                .ValidateOnStart();

            builder.Services
                .AddOptions<OpenClawSettings>()
                .Bind(builder.Configuration.GetSection("OpenClaw"))
                .Validate(
                    settings => !string.IsNullOrWhiteSpace(settings.BaseUrl),
                    "OpenClaw:BaseUrl is required.")
                .Validate(
                    settings => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
                    "OpenClaw:BaseUrl must be a valid absolute URL.")
                .Validate(
                    settings => !string.IsNullOrWhiteSpace(settings.ChatEndpoint),
                    "OpenClaw:ChatEndpoint is required.")
                .Validate(
                    settings => settings.ChatEndpoint.StartsWith("/"),
                    "OpenClaw:ChatEndpoint must start with '/'.")
                .ValidateOnStart();

            builder.Services.AddHttpClient("telegram");
            builder.Services.AddHttpClient<OpenClawClient>();

            builder.Services.AddSingleton<UnauthorizedTracker>();
            builder.Services.AddSingleton<RateLimitTracker>();
            builder.Services.AddSingleton<SessionStateStore>();
            builder.Services.AddHostedService<TelegramBotWorker>();

            using var app = builder.Build();

            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Loading configuration from {ConfigPath}", externalConfigPath);

            try
            {
                await app.RunAsync();
            }
            catch (OptionsValidationException ex)
            {
                logger.LogCritical(
                    ex,
                    "Configuration validation failed for {OptionsType}. Errors: {Errors}",
                    ex.OptionsType.Name,
                    string.Join(" | ", ex.Failures));

                throw;
            }
        }
    }
}