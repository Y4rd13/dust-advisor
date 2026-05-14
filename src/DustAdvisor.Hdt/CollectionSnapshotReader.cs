using System.Collections.Generic;
using System.Threading.Tasks;
using DustAdvisor.Algorithm.Domain;
using Hearthstone_Deck_Tracker.Hearthstone;

namespace DustAdvisor.Hdt
{
    internal static class CollectionSnapshotReader
    {
        public static async Task<IReadOnlyList<CollectionEntry>> ReadAsync(IReadOnlyList<CardMeta> meta)
        {
            var dbfToCardId = new Dictionary<int, string>(meta.Count);
            foreach (var m in meta)
            {
                if (m.DbfId > 0)
                    dbfToCardId[m.DbfId] = m.CardId;
            }

            var collection = await CollectionHelpers.Hearthstone.GetCollection().ConfigureAwait(false);
            if (collection?.Cards == null)
                return new List<CollectionEntry>();

            var result = new List<CollectionEntry>(collection.Cards.Count);
            foreach (var kv in collection.Cards)
            {
                if (!dbfToCardId.TryGetValue(kv.Key, out var cardId))
                    continue;
                var counts = kv.Value;
                if (counts == null || counts.Length < 4) continue;

                // Skip entries where the player owns zero copies of every premium tier.
                // Index 4..7 are TrialCount (loaned, seasonal) — deliberately excluded from
                // ownership counts. If all owned counts are zero, the player has only
                // trials of this card and we treat it as not owned at all.
                if (counts[0] == 0 && counts[1] == 0 && counts[2] == 0 && counts[3] == 0)
                    continue;

                result.Add(new CollectionEntry(
                    cardId: cardId,
                    regular: counts[0],
                    golden: counts[1],
                    diamond: counts[2],
                    signature: counts[3]));
            }
            return result;
        }
    }
}
