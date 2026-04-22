using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TelegramOpenClaw
{
    /// <summary>
    /// Persists per-chat session generation numbers so that /reset survives restarts.
    /// State file: ~/.config/telegram-openclaw/session-state.json
    /// </summary>
    public sealed class SessionStateStore
    {
        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "telegram-openclaw", "session-state.json");

        private readonly Dictionary<long, int> _generations = new();
        private readonly object _sync = new();
        private readonly ILogger<SessionStateStore> _logger;

        public SessionStateStore(ILogger<SessionStateStore> logger)
        {
            _logger = logger;
            Load();
        }

        /// <summary>Returns the current generation for a chat (0 if never reset).</summary>
        public int GetGeneration(long chatId)
        {
            lock (_sync)
            {
                return _generations.TryGetValue(chatId, out var gen) ? gen : 0;
            }
        }

        /// <summary>Increments the generation for a chat and persists the change.</summary>
        public int Increment(long chatId)
        {
            lock (_sync)
            {
                var next = (_generations.TryGetValue(chatId, out var current) ? current : 0) + 1;
                _generations[chatId] = next;
                Save();
                return next;
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(StateFilePath))
                    return;

                var json = File.ReadAllText(StateFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (dict == null) return;

                lock (_sync)
                {
                    foreach (var kv in dict)
                    {
                        if (long.TryParse(kv.Key, out var chatId))
                            _generations[chatId] = kv.Value;
                    }
                }

                _logger.LogInformation("Loaded session-state from {Path} ({Count} entries).", StateFilePath, _generations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load session-state from {Path}; starting fresh.", StateFilePath);
            }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(StateFilePath)!;
                Directory.CreateDirectory(dir);

                var dict = new Dictionary<string, int>();
                foreach (var kv in _generations)
                    dict[kv.Key.ToString()] = kv.Value;

                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StateFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist session-state to {Path}.", StateFilePath);
            }
        }
    }
}
