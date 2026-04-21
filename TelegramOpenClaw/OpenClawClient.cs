using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramOpenClaw.Settings;

namespace TelegramOpenClaw
{
    public sealed class OpenClawClient
    {
        private readonly HttpClient _httpClient;
        private readonly OpenClawSettings _settings;
        private readonly ILogger<OpenClawClient> _logger;
        private readonly Dictionary<long, string> _sessionKeys = new();
        private readonly object _sessionSync = new();

        public string BaseUrl => _settings.BaseUrl;

        public OpenClawClient(
            HttpClient httpClient,
            IOptions<OpenClawSettings> settings,
            ILogger<OpenClawClient> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<string> SendCommandAsync(long chatId, string command, CancellationToken ct)
        {
            var sessionKey = GetSessionKey(chatId);

            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ChatEndpoint);
            request.Headers.Add("x-openclaw-session-key", sessionKey);

            var payload = new
            {
                model = "openclaw/default",
                messages = new object[]
                {
                    new { role = "system", content = "You are OpenClaw, an AI systems control assistant running on a private infrastructure gateway." },
                    new { role = "user", content = command }
                }
            };

            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "OpenClaw request failed for chat {ChatId}, session {SessionKey}: {StatusCode} {Body}",
                    chatId, sessionKey, (int)response.StatusCode, body);

                throw new HttpRequestException($"OpenClaw returned {(int)response.StatusCode}: {body}");
            }

            return TryExtractChatCompletionText(body) ?? body;
        }

        public async Task<bool> IsHealthyAsync(CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.GetAsync("/", ct);
                return response.IsSuccessStatusCode || (int)response.StatusCode < 500;
            }
            catch
            {
                return false;
            }
        }

        public string GetSessionKey(long chatId)
        {
            lock (_sessionSync)
            {
                if (!_sessionKeys.TryGetValue(chatId, out var sessionKey))
                {
                    sessionKey = BuildSessionKey(chatId);
                    _sessionKeys[chatId] = sessionKey;
                }

                return sessionKey;
            }
        }

        private static string? TryExtractChatCompletionText(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array &&
                    choices.GetArrayLength() > 0)
                {
                    var first = choices[0];

                    if (first.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content) &&
                        content.ValueKind == JsonValueKind.String)
                    {
                        return content.GetString();
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string BuildSessionKey(long chatId)
        {
            return $"telegram:{chatId}";
        }
    }
}
