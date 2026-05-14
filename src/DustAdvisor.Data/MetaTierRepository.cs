using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class MetaTierRepository
    {
        private sealed class Entry
        {
            [JsonProperty("cardId")] public string CardId { get; set; }
            [JsonProperty("tier")] public string Tier { get; set; }
        }

        public IReadOnlyDictionary<string, string> Load(string path)
        {
            if (!File.Exists(path)) return new Dictionary<string, string>();
            var raw = File.ReadAllText(path);
            var entries = JsonConvert.DeserializeObject<List<Entry>>(raw) ?? new List<Entry>();
            var map = new Dictionary<string, string>();
            foreach (var e in entries)
            {
                if (!string.IsNullOrEmpty(e.CardId) && !string.IsNullOrEmpty(e.Tier))
                    map[e.CardId] = e.Tier;
            }
            return map;
        }
    }
}
