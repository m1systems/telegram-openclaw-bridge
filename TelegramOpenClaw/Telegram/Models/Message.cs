namespace TelegramOpenClaw.Telegram.Models
{
    public sealed class Message
    {
        public long message_id { get; set; }
        public User? from { get; set; }
        public Chat chat { get; set; }
        public string? text { get; set; }
    }
}
