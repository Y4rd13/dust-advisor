using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Hdt
{
    internal static class CollectionSnapshotReader
    {
        // TAG_PREMIUM values from Hearthstone game engine
        private const int PremiumNormal    = 0;
        private const int PremiumGolden    = 1;
        private const int PremiumDiamond   = 2;
        private const int PremiumSignature = 3;

        public static IReadOnlyList<CollectionEntry> Read()
        {
            var mirror = new HearthMirror.Reflection();
            object raw = mirror.GetFullCollection();
            if (raw == null)
                return new List<CollectionEntry>();

            var enumerable = raw as IEnumerable;
            if (enumerable == null)
                return new List<CollectionEntry>();

            PropertyInfo cardIdProp = null;
            PropertyInfo premiumProp = null;
            PropertyInfo countProp = null;

            var map = new Dictionary<string, int[]>();

            foreach (object card in enumerable)
            {
                if (card == null) continue;

                if (cardIdProp == null)
                {
                    var t = card.GetType();
                    cardIdProp = t.GetProperty("CardId");
                    premiumProp = t.GetProperty("Premium") ?? t.GetProperty("PremiumType");
                    countProp = t.GetProperty("Count");
                    if (cardIdProp == null || premiumProp == null || countProp == null)
                        return new List<CollectionEntry>();
                }

                var cardId = cardIdProp.GetValue(card) as string;
                if (string.IsNullOrEmpty(cardId)) continue;

                int premium = System.Convert.ToInt32(premiumProp.GetValue(card));
                int count = System.Convert.ToInt32(countProp.GetValue(card));

                if (!map.TryGetValue(cardId, out int[] counts))
                {
                    counts = new int[4];
                    map[cardId] = counts;
                }

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
