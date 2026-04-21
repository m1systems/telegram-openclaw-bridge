using TelegramOpenClaw.Settings;

namespace TelegramOpenClaw
{
    public sealed class RateLimitTracker
    {
        private readonly Dictionary<long, SlidingWindowCounter> _windows = new();
        private readonly object _sync = new();

        public bool TryConsume(long userId, RateLimitSettings settings, out bool isTemporarilyMuted)
        {
            lock (_sync)
            {
                if (!_windows.TryGetValue(userId, out var counter))
                {
                    counter = new SlidingWindowCounter();
                    _windows[userId] = counter;
                }

                var now = DateTimeOffset.UtcNow;

                if (counter.MutedUntilUtc.HasValue && counter.MutedUntilUtc.Value > now)
                {
                    isTemporarilyMuted = true;
                    return false;
                }

                if (counter.WindowStartUtc == default ||
                    now - counter.WindowStartUtc >= TimeSpan.FromSeconds(settings.WindowSeconds))
                {
                    counter.WindowStartUtc = now;
                    counter.Count = 0;
                }

                counter.Count++;

                if (counter.Count > settings.MaxCommandsPerWindow)
                {
                    counter.MutedUntilUtc = now.AddMinutes(settings.ViolationMuteMinutes);
                    isTemporarilyMuted = false;
                    return false;
                }

                isTemporarilyMuted = false;
                return true;
            }
        }

        private sealed class SlidingWindowCounter
        {
            public DateTimeOffset WindowStartUtc { get; set; }
            public int Count { get; set; }
            public DateTimeOffset? MutedUntilUtc { get; set; }
        }
    }
}
