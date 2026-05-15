using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class RefundRepository
    {
        private sealed class Entry
        {
            [JsonProperty("cardId")] public string CardId { get; set; }
            [JsonProperty("expiresUtc")] public DateTimeOffset ExpiresUtc { get; set; }
        }

        public IReadOnlyCollection<string> Load(string path, DateTimeOffset now)
        {
            var raw = File.ReadAllText(path);
            var entries = JsonConvert.DeserializeObject<List<Entry>>(raw) ?? new List<Entry>();
            var set = new HashSet<string>();
            foreach (var e in entries)
            {
                if (e.ExpiresUtc > now) set.Add(e.CardId);
            }
            return set;
        }
    }
}
