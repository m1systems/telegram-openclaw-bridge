namespace TelegramOpenClaw.Settings
{
    public sealed class TelegramSettings
    {
        public string BotToken { get; set; } = string.Empty;
        public List<long> AllowedUserIds { get; set; } = new();
        public UnauthorizedSettings Unauthorized { get; set; } = new();
        public PollingSettings Polling { get; set; } = new();
        public RateLimitSettings RateLimit { get; set; } = new();
    }
}
