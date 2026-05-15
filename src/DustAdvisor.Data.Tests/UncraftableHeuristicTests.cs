using DustAdvisor.Algorithm.Domain;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class UncraftableHeuristicTests
    {
        [Theory]
        [InlineData("Earned from the Tavern Pass.", true)]
        [InlineData("Reward for completing the Rewards Track.", true)]
        [InlineData("Awarded via Achievement.", true)]
        [InlineData("Twitch drop, season 4.", true)]
        [InlineData("Hero Skin bundle exclusive.", true)]
        [InlineData("Promotional code.", true)]
        [InlineData("Group Learning new-player gift.", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("Found in card packs.", false)]
        [InlineData("Whizbang's Workshop expansion.", false)]
        [InlineData("Earnable after opening a Whizbang's Workshop card pack.", true)]
        [InlineData("Earnable after purchasing the Tavern Pass.", true)]
        [InlineData("Catch-Up Pack legendary.", true)]
        [InlineData("Pre-order exclusive.", true)]
        [InlineData("Free Reward Track gift.", true)]
        // Verified against HearthstoneJSON cards.collectible.json patterns:
        [InlineData("Unlocked with the Demon Hunter class.", true)]
        [InlineData("Unlocked with Warlock class.", true)]
        [InlineData("Unlocked with \"Congealing Essence\" Achievement.", true)]
        [InlineData("Unlocked when opening a Forged in the Barrens pack.", true)]
        [InlineData("Unlocked when you have all the Murlocs from the Legacy Set.", true)]
        [InlineData("Unlocked by completing the Tutorial.", true)]
        [InlineData("Unlocked by opening a Kobolds & Catacombs pack.", true)]
        [InlineData("Unlocked after completing the starter quests.", true)]
        [InlineData("Unlocked after completing Apprentice.", true)]
        [InlineData("Unlocked with the BlizzCon 2017 virtual ticket.", true)]
        // Adventure unlocks must NOT match (these ARE craftable after unlock):
        [InlineData("Unlocked in Blackrock Spire, in the Blackrock Mountain adventure.", false)]
        [InlineData("Unlocked in the Hall of Explorers, in the League of Explorers adventure.", false)]
        [InlineData("Unlocked in the Spire, in One Night in Karazhan.", false)]
        [InlineData("Unlocked by starting the League of Explorers adventure.", false)]
        // "Must be crafted" cards are craftable, must not match:
        [InlineData("Not available in packs, must be crafted.", false)]
        public void LooksUncraftable_recognizes_canonical_phrases(string howToEarn, bool expected)
        {
            UncraftableHeuristic.LooksUncraftable(howToEarn).Should().Be(expected);
        }

        [Fact]
        public void DetectUncraftablePremiums_flags_regular_only_when_howToEarn_matches()
        {
            var result = UncraftableHeuristic.DetectUncraftablePremiums(
                howToEarn: "Earned from Tavern Pass.",
                howToEarnGolden: "Found in packs.");
            result.Should().BeEquivalentTo(new[] { Premium.Regular });
        }

        [Fact]
        public void DetectUncraftablePremiums_flags_both_when_both_match()
        {
            var result = UncraftableHeuristic.DetectUncraftablePremiums(
                howToEarn: "Earned from Achievement.",
                howToEarnGolden: "Earned from Achievement (golden).");
            result.Should().BeEquivalentTo(new[] { Premium.Regular, Premium.Golden });
        }

        [Fact]
        public void DetectUncraftablePremiums_returns_empty_when_neither_matches()
        {
            UncraftableHeuristic.DetectUncraftablePremiums(null, null).Should().BeEmpty();
        }
    }
}
