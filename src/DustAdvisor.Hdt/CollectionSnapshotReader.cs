using System.Collections.Generic;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Hdt
{
    internal static class CollectionSnapshotReader
    {
        // TAG_PREMIUM values from Hearthstone game engine (PremiumType enum)
        private const int PremiumNormal    = 0;
        private const int PremiumGolden    = 1;
        private const int PremiumDiamond   = 2;
        private const int PremiumSignature = 3;

        public static IReadOnlyList<CollectionEntry> Read()
        {
            var mirror = new HearthMirror.Reflection();
            dynamic raw = mirror.GetFullCollection();
            if (raw == null)
                return new List<CollectionEntry>();

            // Group by CardId and sum counts by premium type.
            // Using dynamic because HearthMirror.Objects.Card properties are not
            // accessible as typed C# properties in this build of HearthMirror.dll.
            var map = new Dictionary<string, int[]>(); // [normal, golden, diamond, signature]

            foreach (dynamic card in raw)
            {
                if (card == null)
                    continue;

                string cardId = (string)card.CardId;
                if (string.IsNullOrEmpty(cardId))
                    continue;

                if (!map.TryGetValue(cardId, out int[] counts))
                {
                    counts = new int[4];
                    map[cardId] = counts;
                }

                int premium = (int)card.Premium;
                int count   = (int)card.Count;

                switch (premium)
                {
                    case PremiumNormal:    counts[0] += count; break;
                    case PremiumGolden:    counts[1] += count; break;
                    case PremiumDiamond:   counts[2] += count; break;
                    case PremiumSignature: counts[3] += count; break;
                }
            }

            var result = new List<CollectionEntry>(map.Count);
            foreach (var kv in map)
            {
                result.Add(new CollectionEntry(
                    cardId:    kv.Key,
                    regular:   kv.Value[0],
                    golden:    kv.Value[1],
                    diamond:   kv.Value[2],
                    signature: kv.Value[3]));
            }
            return result;
        }
    }
}
