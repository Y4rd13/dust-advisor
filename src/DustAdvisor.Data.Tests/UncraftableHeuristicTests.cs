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
