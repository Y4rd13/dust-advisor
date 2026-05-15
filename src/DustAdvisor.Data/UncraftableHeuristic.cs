using System;
using System.Collections.Generic;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Data
{
    /// Heuristically detects which (cardId, Premium) copies are uncraftable based on
    /// the howToEarn / howToEarnGolden text from HearthstoneJSON. A copy is flagged
    /// uncraftable if Blizzard distributes it only through non-pack channels (Tavern
    /// Pass, Rewards Track, Achievements, Hero Skin bundles, Twitch drops, promos,
    /// the Group Learning new-player experience).
    public static class UncraftableHeuristic
    {
        // Substrings (case-insensitive) on HearthstoneJSON's howToEarn/howToEarnGolden that
        // mark a (cardId, premium) copy as uncraftable. Verified against the full
        // cards.collectible.json — patterns chosen to NOT match adventure unlocks
        // ("Unlocked in X, in the Y adventure" or "Unlocked by starting the Z adventure"),
        // which ARE craftable/disenchantable after unlock.
        private static readonly string[] UncraftablePhrases =
        {
            "tavern pass",
            "rewards track",
            "reward track",
            "achievement",
            "twitch",
            "promotional",
            "promo code",
            "hero skin",
            "bundle",
            "group learning",
            "trial",
            "earnable",
            "catch-up",
            "catch up pack",
            "free reward",
            "pre-purchase",
            "prepurchase",
            "preorder",
            "pre-order",
            "unlocked with ",
            "unlocked when ",
            "unlocked by completing ",
            "unlocked by opening ",
            "unlocked after completing ",
            "blizzcon",
            "virtual ticket",
        };

        public static bool LooksUncraftable(string howToEarnText)
        {
            if (string.IsNullOrEmpty(howToEarnText)) return false;
            var s = howToEarnText.ToLowerInvariant();
            foreach (var phrase in UncraftablePhrases)
            {
                if (s.IndexOf(phrase, StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        public static IEnumerable<Premium> DetectUncraftablePremiums(string howToEarn, string howToEarnGolden)
        {
            if (LooksUncraftable(howToEarn))
                yield return Premium.Regular;
            if (LooksUncraftable(howToEarnGolden))
                yield return Premium.Golden;
        }
    }
}
