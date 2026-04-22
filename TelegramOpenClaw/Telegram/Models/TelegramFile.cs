namespace TelegramOpenClaw.Telegram.Models
{
    public sealed class TelegramFile
    {
        public string file_id { get; set; } = string.Empty;
        public string file_unique_id { get; set; } = string.Empty;
        public long? file_size { get; set; }
        public string? file_path { get; set; }
    }
}
