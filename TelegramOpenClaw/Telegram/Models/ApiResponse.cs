namespace TelegramOpenClaw.Telegram.Models
{
    public sealed class ApiResponse<T>
    {
        public bool ok { get; set; }
        public T? result { get; set; }
        public string? description { get; set; }
    }
}
