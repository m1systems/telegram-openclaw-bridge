namespace TelegramOpenClaw.Telegram.Models
{
    public sealed class Update
    {
        public long update_id { get; set; }
        public Message? message { get; set; }
    }
}
