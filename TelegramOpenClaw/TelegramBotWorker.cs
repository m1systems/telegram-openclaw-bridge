using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramOpenClaw.Settings;
using TelegramOpenClaw.Telegram.Models;

namespace TelegramOpenClaw
{
    public sealed class TelegramBotWorker : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OpenClawClient _openClawClient;
        private readonly IOptionsMonitor<TelegramSettings> _settingsMonitor;
        private readonly UnauthorizedTracker _unauthorizedTracker;
        private readonly RateLimitTracker _rateLimitTracker;
        private readonly ILogger<TelegramBotWorker> _logger;
        private long _offset;
        private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
        private DateTimeOffset? _lastSuccessfulOpenClawCallUtc;

        public TelegramBotWorker(
            IHttpClientFactory httpClientFactory,
            OpenClawClient openClawClient,
            IOptionsMonitor<TelegramSettings> settingsMonitor,
            UnauthorizedTracker unauthorizedTracker,
            RateLimitTracker rateLimitTracker,
            ILogger<TelegramBotWorker> logger)
        {
            _httpClientFactory = httpClientFactory;
            _openClawClient = openClawClient;
            _settingsMonitor = settingsMonitor;
            _unauthorizedTracker = unauthorizedTracker;
            _rateLimitTracker = rateLimitTracker;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Telegram bot worker starting.");

            var me = await GetMeAsync(stoppingToken);
            if (me != null)
                _logger.LogInformation("Connected as bot @{Username} (id {BotId}).", me.username, me.id);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var settings = _settingsMonitor.CurrentValue;
                    var updates = await GetUpdatesAsync(settings, stoppingToken);

                    if (updates.Count == 0)
                    {
                        await Task.Delay(settings.Polling.IdleDelayMilliseconds, stoppingToken);
                        continue;
                    }

                    foreach (var update in updates)
                    {
                        _offset = update.update_id + 1;

                        var message = update.message;
                        if (message == null)
                            continue;

                        if (message.photo != null && message.photo.Length > 0)
                        {
                            await HandlePhotoAsync(message, settings, stoppingToken);
                            _lastSuccessfulOpenClawCallUtc = DateTimeOffset.UtcNow;
                        }
                        else if (message.text != null)
                        {
                            await HandleMessageAsync(message, settings, stoppingToken);
                            _lastSuccessfulOpenClawCallUtc = DateTimeOffset.UtcNow;
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in Telegram polling loop.");
                    await Task.Delay(2000, stoppingToken);
                }
            }

            _logger.LogInformation("Telegram bot worker stopping.");
        }

        private async Task HandleMessageAsync(Message message, TelegramSettings settings, CancellationToken ct)
        {
            var chatId = message.chat.id;
            var userId = message.from?.id ?? 0;
            var username = message.from?.username ?? "<unknown>";
            var text = (message.text ?? string.Empty).Trim();

            _logger.LogInformation(
                "Incoming message from user {UserId} (@{Username}) in chat {ChatId}: {Text}",
                userId, username, chatId, text);

            if (!settings.AllowedUserIds.Contains(userId))
            {
                var shouldRespond = settings.Unauthorized.RespondOnce && _unauthorizedTracker.ShouldRespond(userId);

                if (shouldRespond)
                {
                    await SendMessageAsync(settings, chatId, settings.Unauthorized.ResponseText, ct);
                    _logger.LogWarning("Unauthorized user {UserId} received one-time rejection response.", userId);

                    if (settings.Unauthorized.PermanentlyMuteAfterFirstResponse)
                        _unauthorizedTracker.MarkMuted(userId);
                }
                else
                {
                    _logger.LogWarning("Ignoring unauthorized user {UserId} silently.", userId);
                }

                return;
            }

            if (!_rateLimitTracker.TryConsume(userId, settings.RateLimit, out var isTemporarilyMuted))
            {
                if (!isTemporarilyMuted)
                {
                    await SendMessageAsync(settings, chatId, settings.RateLimit.ViolationResponseText, ct);
                    _logger.LogWarning("Rate limit exceeded for user {UserId}; warning sent.", userId);
                }
                else
                {
                    _logger.LogWarning("Temporarily muted rate-limited user {UserId}; ignoring.", userId);
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogInformation("Ignoring empty message from user {UserId}.", userId);
                return;
            }

            try
            {
                if (text.StartsWith("/"))
                {
                    var handled = await HandleLocalCommandAsync(settings, chatId, userId, text, ct);
                    if (handled)
                        return;

                    await SendMessageAsync(
                        settings,
                        chatId,
                        "Unknown command. Available commands: /help, /status, /reset",
                        ct);

                    return;
                }

                var response = await _openClawClient.SendCommandAsync(chatId, text, ct);

                if (string.IsNullOrWhiteSpace(response))
                    response = "(No response)";

                await SendMessageAsync(settings, chatId, response, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing Telegram message from user {UserId}.", userId);
                await SendMessageAsync(settings, chatId, "Command failed.", ct);
            }
        }

        private async Task HandlePhotoAsync(Message message, TelegramSettings settings, CancellationToken ct)
        {
            var chatId = message.chat.id;
            var userId = message.from?.id ?? 0;
            var username = message.from?.username ?? "<unknown>";
            var caption = message.caption?.Trim() ?? string.Empty;

            _logger.LogInformation(
                "Incoming photo from user {UserId} (@{Username}) in chat {ChatId}. Caption: {Caption}",
                userId, username, chatId, caption);

            if (!settings.AllowedUserIds.Contains(userId))
            {
                var shouldRespond = settings.Unauthorized.RespondOnce && _unauthorizedTracker.ShouldRespond(userId);

                if (shouldRespond)
                {
                    await SendMessageAsync(settings, chatId, settings.Unauthorized.ResponseText, ct);
                    _logger.LogWarning("Unauthorized user {UserId} received one-time rejection response.", userId);

                    if (settings.Unauthorized.PermanentlyMuteAfterFirstResponse)
                        _unauthorizedTracker.MarkMuted(userId);
                }
                else
                {
                    _logger.LogWarning("Ignoring unauthorized user {UserId} silently.", userId);
                }

                return;
            }

            if (!_rateLimitTracker.TryConsume(userId, settings.RateLimit, out var isTemporarilyMuted))
            {
                if (!isTemporarilyMuted)
                {
                    await SendMessageAsync(settings, chatId, settings.RateLimit.ViolationResponseText, ct);
                    _logger.LogWarning("Rate limit exceeded for user {UserId}; warning sent.", userId);
                }
                else
                {
                    _logger.LogWarning("Temporarily muted rate-limited user {UserId}; ignoring.", userId);
                }

                return;
            }

            try
            {
                // Select highest-resolution photo (last element has the largest dimensions)
                var bestPhoto = message.photo!.OrderByDescending(p => p.width * p.height).First();

                // Resolve file path via Telegram getFile API
                var telegramClient = _httpClientFactory.CreateClient("telegram");
                var getFileUrl = BuildTelegramUrl(settings.BotToken, $"getFile?file_id={bestPhoto.file_id}");
                var fileResponse = await telegramClient.GetFromJsonAsync<ApiResponse<TelegramFile>>(getFileUrl, ct);

                if (fileResponse?.result?.file_path == null)
                {
                    _logger.LogError("Failed to resolve file path for photo from user {UserId}.", userId);
                    await SendMessageAsync(settings, chatId, "Failed to retrieve image.", ct);
                    return;
                }

                var filePath = fileResponse.result.file_path;
                var downloadUrl = $"https://api.telegram.org/file/bot{settings.BotToken}/{filePath}";

                // Download image bytes
                var imageBytes = await telegramClient.GetByteArrayAsync(downloadUrl, ct);

                // Determine MIME type from file extension
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var mimeType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

                var response = await _openClawClient.SendImageAsync(chatId, imageBytes, mimeType, caption, ct);

                if (string.IsNullOrWhiteSpace(response))
                    response = "(No response)";

                await SendMessageAsync(settings, chatId, response, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing photo from user {UserId}.", userId);
                await SendMessageAsync(settings, chatId, "Image processing failed.", ct);
            }
        }

        private async Task<bool> HandleLocalCommandAsync(
            TelegramSettings settings,
            long chatId,
            long userId,
            string text,
            CancellationToken ct)
        {
            var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].Trim().ToLowerInvariant();

            switch (command)
            {
                case "/help":
                    await SendMessageAsync(
                        settings,
                        chatId,
                        "Available commands:\n" +
                        "/help - Show this help\n" +
                        "/status - Show bridge/session status\n" +
                        "/reset - Start a fresh OpenClaw session for this chat",
                        ct);
                    return true;

                case "/status":
                    {
                        var sessionKey = _openClawClient.GetSessionKey(chatId);
                        var ocHealthy = await _openClawClient.IsHealthyAsync(ct);
                        var uptime = DateTimeOffset.UtcNow - _startedAtUtc;

                        var statusText =
                            "Bridge online.\n" +
                            $"UserId: {userId}\n" +
                            $"ChatId: {chatId}\n" +
                            $"SessionKey: {sessionKey}\n" +
                            $"OpenClaw: {(_openClawClient.BaseUrl)}\n" +
                            $"OpenClawReachable: {(ocHealthy ? "yes" : "no")}\n" +
                            $"StartedUtc: {_startedAtUtc:O}\n" +
                            $"Uptime: {uptime:c}\n" +
                            $"LastSuccessfulOpenClawCallUtc: {(_lastSuccessfulOpenClawCallUtc.HasValue ? _lastSuccessfulOpenClawCallUtc.Value.ToString("O") : "never")}";

                        await SendMessageAsync(settings, chatId, statusText, ct);
                        return true;
                    }

                case "/reset":
                    _openClawClient.ResetSession(chatId);
                    await SendMessageAsync(settings, chatId, "Session reset. Starting fresh.", ct);
                    return true;

                default:
                    return false;
            }
        }

        private async Task<BotIdentity?> GetMeAsync(CancellationToken ct)
        {
            var settings = _settingsMonitor.CurrentValue;
            var client = _httpClientFactory.CreateClient("telegram");
            var url = BuildTelegramUrl(settings.BotToken, "getMe");

            var response = await client.GetFromJsonAsync<ApiResponse<BotIdentity>>(url, ct);
            return response?.result;
        }

        private async Task<List<Update>> GetUpdatesAsync(TelegramSettings settings, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("telegram");
            var url = BuildTelegramUrl(
                settings.BotToken,
                $"getUpdates?offset={_offset}&timeout={settings.Polling.TimeoutSeconds}");

            var response = await client.GetFromJsonAsync<ApiResponse<List<Update>>>(url, ct);

            if (response == null)
                return new List<Update>();

            if (!response.ok)
                throw new Exception($"Telegram getUpdates failed: {response.description ?? "unknown error"}");

            return response.result ?? new List<Update>();
        }

        private async Task SendMessageAsync(TelegramSettings settings, long chatId, string text, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("telegram");
            var url = BuildTelegramUrl(settings.BotToken, "sendMessage");

            var payload = new
            {
                chat_id = chatId,
                text
            };

            using var response = await client.PostAsJsonAsync(url, payload, ct);
            response.EnsureSuccessStatusCode();
        }

        private static string BuildTelegramUrl(string botToken, string method)
            => $"https://api.telegram.org/bot{botToken}/{method}";
    }
}
