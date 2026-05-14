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

                bool refund = inputs.RefundWindow.Contains(meta.CardId);
                if (inputs.Options.Strategy == Strategy.RefundOnly && !refund) continue;

                if (meta.Set.IsStandardLegal)
                {
                    warnings.Add(new Warning(meta.CardId,
                        "Standard-legal: card may still be valuable in current meta."));
                    if (inputs.Options.Strategy == Strategy.SafeOnly && inputs.Options.KeepStandardLegal) continue;
                }

                int playset = Constants.PlaysetSize(meta.Rarity);
                int cosmeticHeld = entry.Diamond + entry.Signature;
                int playsetRemaining = System.Math.Max(0, playset - cosmeticHeld);

                bool regularLocked = inputs.Uncraftable.Contains((meta.CardId, Premium.Regular));
                bool goldenLocked = inputs.Uncraftable.Contains((meta.CardId, Premium.Golden));
                int effectiveRegular = regularLocked ? 0 : entry.Regular;
                int effectiveGolden = goldenLocked ? 0 : entry.Golden;

                int keepGolden = System.Math.Min(effectiveGolden, playsetRemaining);
                int keepRegular = System.Math.Max(0, playsetRemaining - keepGolden);
                int dustRegular = System.Math.Max(0, effectiveRegular - keepRegular);
                int dustGolden = System.Math.Max(0, effectiveGolden - keepGolden);

                int unitRegular = refund ? Constants.CraftCost(meta.Rarity) : Constants.DisenchantRegular(meta.Rarity);
                int unitGolden = refund ? Constants.GoldenCraftCost(meta.Rarity) : Constants.DisenchantGolden(meta.Rarity);
                int dustGained = dustRegular * unitRegular + dustGolden * unitGolden;

                if (dustRegular > 0 || dustGolden > 0)
                {
                    items.Add(new DustItem(
                        cardId: meta.CardId,
                        cardName: meta.Name,
                        rarity: meta.Rarity,
                        regularToDust: dustRegular,
                        goldenToDust: dustGolden,
                        dustGained: dustGained,
                        inRefundWindow: refund,
                        isStandardLegal: meta.Set.IsStandardLegal));
                    totalDust += dustGained;
                }
            }

            var ordered = items
                .OrderByDescending(i => i.InRefundWindow)
                .ThenBy(i => i.IsStandardLegal)
                .ThenByDescending(i => i.Rarity)
                .ThenBy(i => i.CardName)
                .ToList();

            return new DustPlan(ordered, warnings, totalDust);
        }
    }
}
