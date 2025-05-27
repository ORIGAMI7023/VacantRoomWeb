namespace VacantRoomWeb
{
    /// <summary>
    /// 用于在用户连接时记录 IP，在断开时查出对应 IP
    /// </summary>
    public class ClientConnectionTracker
    {
        private readonly Dictionary<string, string> _ipMap = new();
        private readonly object _lock = new();

        public void SetClientIp(string circuitId, string ip)
        {
            lock (_lock)
            {
                _ipMap[circuitId] = ip;
            }
        }

        public string? GetClientIp(string circuitId)
        {
            lock (_lock)
            {
                return _ipMap.TryGetValue(circuitId, out var ip) ? ip : null;
            }
        }

        public void Remove(string circuitId)
        {
            lock (_lock)
            {
                _ipMap.Remove(circuitId);
            }
        }
    }

}
