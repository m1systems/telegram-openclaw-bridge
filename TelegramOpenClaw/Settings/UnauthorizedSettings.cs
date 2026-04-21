namespace TelegramOpenClaw.Settings
{
    public sealed class UnauthorizedSettings
    {
        public bool RespondOnce { get; set; } = true;
        public string ResponseText { get; set; } = "Unauthorized.";
        public bool PermanentlyMuteAfterFirstResponse { get; set; } = true;
    }
}
