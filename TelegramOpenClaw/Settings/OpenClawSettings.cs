namespace TelegramOpenClaw.Settings
{
    public sealed class OpenClawSettings
    {
        public string BaseUrl { get; set; } = "http://127.0.0.1:18789";
        public string ChatEndpoint { get; set; } = "/v1/chat/completions";
    }
}
