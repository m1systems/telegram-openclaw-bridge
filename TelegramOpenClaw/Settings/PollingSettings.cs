namespace TelegramOpenClaw.Settings
{
    public sealed class PollingSettings
    {
        public int TimeoutSeconds { get; set; } = 25;
        public int IdleDelayMilliseconds { get; set; } = 500;
    }
}
