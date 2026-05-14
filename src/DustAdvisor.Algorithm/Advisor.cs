using System.Collections.Generic;
using System.Linq;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm
{
    public sealed class Advisor
    {
        public DustPlan Recommend(AdvisorInputs inputs)
        {
            var metaById = inputs.Meta.ToDictionary(m => m.CardId);
            var items = new List<DustItem>();
            var warnings = new List<Warning>();
            var totalDust = 0;

            foreach (var entry in inputs.Collection)
            {
                if (!metaById.TryGetValue(entry.CardId, out var meta)) continue;
                if (!meta.IsCollectible) continue;
                if (meta.Rarity == Rarity.Free) continue;
                if (meta.Set.IsCore) continue;

                int playset = Constants.PlaysetSize(meta.Rarity);

                // Diamond and Signature count toward playset but cannot be dusted (Diamond never;
                // Signature only via the uncraftable set in a later task).
                int cosmeticHeld = entry.Diamond + entry.Signature;
                int playsetRemaining = System.Math.Max(0, playset - cosmeticHeld);

                int keepGolden = System.Math.Min(entry.Golden, playsetRemaining);
                int keepRegular = System.Math.Max(0, playsetRemaining - keepGolden);
                int dustRegular = System.Math.Max(0, entry.Regular - keepRegular);
                int dustGolden = System.Math.Max(0, entry.Golden - keepGolden);

                int dustGained = dustRegular * Constants.DisenchantRegular(meta.Rarity)
                               + dustGolden * Constants.DisenchantGolden(meta.Rarity);

                if (dustRegular > 0 || dustGolden > 0)
                {
                    items.Add(new DustItem(
                        cardId: meta.CardId,
                        cardName: meta.Name,
                        rarity: meta.Rarity,
                        regularToDust: dustRegular,
                        goldenToDust: dustGolden,
                        dustGained: dustGained,
                        inRefundWindow: false,
                        isStandardLegal: meta.Set.IsStandardLegal));
                    totalDust += dustGained;
                }
            }

            return new DustPlan(items, warnings, totalDust);
        }
    }
}
