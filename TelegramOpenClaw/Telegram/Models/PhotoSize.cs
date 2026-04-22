namespace TelegramOpenClaw.Telegram.Models
{
    public sealed class PhotoSize
    {
        public string file_id { get; set; } = string.Empty;
        public string file_unique_id { get; set; } = string.Empty;
        public int width { get; set; }
        public int height { get; set; }
        public long? file_size { get; set; }
    }
}
