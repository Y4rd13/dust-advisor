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
