namespace TelegramOpenClaw
{
    public sealed class UnauthorizedTracker
    {
        private readonly HashSet<long> _respondedUsers = new();
        private readonly HashSet<long> _mutedUsers = new();
        private readonly object _sync = new();

        public bool ShouldRespond(long userId)
        {
            lock (_sync)
            {
                if (_mutedUsers.Contains(userId))
                    return false;

                if (_respondedUsers.Contains(userId))
                    return false;

                _respondedUsers.Add(userId);
                return true;
            }
        }

        public void MarkMuted(long userId)
        {
            lock (_sync)
            {
                _mutedUsers.Add(userId);
            }
        }
    }
}
