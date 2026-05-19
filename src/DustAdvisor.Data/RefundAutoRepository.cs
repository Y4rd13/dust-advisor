using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class RefundAutoRepository
    {
        private sealed class ChangeDto
        {
            [JsonProperty("field")] public string Field { get; set; }
            [JsonProperty("oldValue")] public string OldValue { get; set; }
            [JsonProperty("newValue")] public string NewValue { get; set; }
            [JsonProperty("direction")] public string Direction { get; set; }
        }

        private sealed class EntryDto
        {
            [JsonProperty("cardId")] public string CardId { get; set; }
            [JsonProperty("cardName")] public string CardName { get; set; }
            [JsonProperty("expiresUtc")] public DateTimeOffset ExpiresUtc { get; set; }
            [JsonProperty("detectedAt")] public DateTimeOffset DetectedAt { get; set; }
            [JsonProperty("changes")] public List<ChangeDto> Changes { get; set; }
        }

        public IReadOnlyList<RefundEntry> Load(string path, DateTimeOffset now)
        {
            if (!File.Exists(path)) return System.Array.Empty<RefundEntry>();

            var raw = File.ReadAllText(path);
            var dtos = JsonConvert.DeserializeObject<List<EntryDto>>(raw) ?? new List<EntryDto>();

            var result = new List<RefundEntry>();
            foreach (var d in dtos)
            {
                if (string.IsNullOrEmpty(d.CardId)) continue;
                if (d.ExpiresUtc <= now) continue;
                var changes = (d.Changes ?? new List<ChangeDto>())
                    .Select(c => new PatchChange(
                        d.CardId, d.CardName ?? d.CardId, c.Field ?? "", c.OldValue ?? "", c.NewValue ?? "",
                        ParseDirection(c.Direction)))
                    .ToList();
                result.Add(new RefundEntry(d.CardId, d.CardName ?? d.CardId, d.ExpiresUtc, d.DetectedAt, changes));
            }
            return result;
        }

        public void Save(string path, IEnumerable<RefundEntry> entries)
        {
            var dtos = entries.Select(e => new EntryDto
            {
                CardId = e.CardId,
                CardName = e.CardName,
                ExpiresUtc = e.ExpiresUtc,
                DetectedAt = e.DetectedAt,
                Changes = e.Changes.Select(c => new ChangeDto
                {
                    Field = c.FieldName,
                    OldValue = c.OldValue,
                    NewValue = c.NewValue,
                    Direction = c.Direction.ToString(),
                }).ToList(),
            }).ToList();

            var json = JsonConvert.SerializeObject(dtos, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Merges existing refund entries with newly-detected patch changes:
        ///   1. Drops entries whose expiresUtc &lt;= now (cleanup).
        ///   2. Groups newChanges by CardId; each group becomes one RefundEntry with the
        ///      new expiresUtc.
        ///   3. New entries replace existing entries with the same CardId (newer patch wins).
        /// Returns a fresh list; does not mutate inputs.
        /// </summary>
        public static IReadOnlyList<RefundEntry> MergeWithNewPatch(
            IEnumerable<RefundEntry> existing,
            IEnumerable<PatchChange> newChanges,
            DateTimeOffset expiresUtc,
            DateTimeOffset detectedAt,
            DateTimeOffset now)
        {
            var byCardId = new Dictionary<string, RefundEntry>();

            foreach (var e in existing)
            {
                if (e.ExpiresUtc <= now) continue;
                byCardId[e.CardId] = e;
            }

            var grouped = newChanges
                .Where(c => !string.IsNullOrEmpty(c.CardId))
                .GroupBy(c => c.CardId);

            foreach (var group in grouped)
            {
                var changes = group.ToList();
                var name = changes[0].CardName ?? group.Key;
                byCardId[group.Key] = new RefundEntry(group.Key, name, expiresUtc, detectedAt, changes);
            }

            return byCardId.Values.ToList();
        }

        private static PatchChangeDirection ParseDirection(string s)
        {
            switch (s)
            {
                case "Nerf": return PatchChangeDirection.Nerf;
                case "Buff": return PatchChangeDirection.Buff;
                default: return PatchChangeDirection.Neutral;
            }
        }
    }
}
