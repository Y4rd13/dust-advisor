using System.Collections.Generic;
using System.IO;
using DustAdvisor.Algorithm.Domain;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class NeverSuggestRepository
    {
        private sealed class Entry
        {
            [JsonProperty("cardId")] public string CardId { get; set; }
            [JsonProperty("premium")] public string Premium { get; set; }
        }

        public IReadOnlyCollection<(string CardId, Premium Premium)> Load(string path)
        {
            if (!File.Exists(path))
                return new HashSet<(string, Premium)>();

            var raw = File.ReadAllText(path);
            var entries = JsonConvert.DeserializeObject<List<Entry>>(raw) ?? new List<Entry>();
            var set = new HashSet<(string, Premium)>();
            foreach (var e in entries)
            {
                if (System.Enum.TryParse<Premium>(e.Premium, ignoreCase: true, out var p))
                    set.Add((e.CardId, p));
            }
            return set;
        }

        public void Save(string path, System.Collections.Generic.IEnumerable<(string CardId, Premium Premium)> entries)
        {
            var list = new List<Entry>();
            foreach (var (cardId, premium) in entries)
                list.Add(new Entry { CardId = cardId, Premium = premium.ToString() });

            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}
