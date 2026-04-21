namespace TelegramOpenClaw.Settings
{
    public sealed class RateLimitSettings
    {
        public int WindowSeconds { get; set; } = 60;
        public int MaxCommandsPerWindow { get; set; } = 10;
        public int ViolationMuteMinutes { get; set; } = 10;
        public string ViolationResponseText { get; set; } = "Rate limit exceeded. Try again later.";
    }
}
