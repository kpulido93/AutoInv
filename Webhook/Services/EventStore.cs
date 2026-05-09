using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Services
{
    public record EventSummary(DateTimeOffset ReceivedAtUtc, string ClientId, int SizeBytes, string Sha256);

    public class EventStore
    {
        private readonly ConcurrentQueue<EventSummary> _events = new();

        public void AddEvent(string clientId, string raw)
        {
            raw ??= "";
            var bytes = Encoding.UTF8.GetBytes(raw);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));

            _events.Enqueue(new EventSummary(DateTimeOffset.UtcNow, clientId ?? "N/A", bytes.Length, hash));

            while (_events.Count > 50)
                _events.TryDequeue(out _);
        }

        public IEnumerable<EventSummary> GetEvents() => _events.ToArray();
    }
}
