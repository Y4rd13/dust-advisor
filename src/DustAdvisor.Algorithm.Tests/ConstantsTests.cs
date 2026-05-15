using DustAdvisor.Algorithm;
using DustAdvisor.Algorithm.Domain;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Algorithm.Tests
{
    public class ConstantsTests
    {
        [Theory]
        [InlineData(Rarity.Common, 5)]
        [InlineData(Rarity.Rare, 20)]
        [InlineData(Rarity.Epic, 100)]
        [InlineData(Rarity.Legendary, 400)]
        public void DisenchantRegular_returns_canonical_values(Rarity r, int expected)
        {
            Constants.DisenchantRegular(r).Should().Be(expected);
        }

        [Theory]
        [InlineData(Rarity.Common, 50)]
        [InlineData(Rarity.Rare, 100)]
        [InlineData(Rarity.Epic, 400)]
        [InlineData(Rarity.Legendary, 1600)]
        public void DisenchantGolden_returns_canonical_values(Rarity r, int expected)
        {
            Constants.DisenchantGolden(r).Should().Be(expected);
        }

        [Theory]
        [InlineData(Rarity.Common, 40)]
        [InlineData(Rarity.Rare, 100)]
        [InlineData(Rarity.Epic, 400)]
        [InlineData(Rarity.Legendary, 1600)]
        public void CraftCost_returns_canonical_values(Rarity r, int expected)
        {
            Constants.CraftCost(r).Should().Be(expected);
        }

        [Theory]
        [InlineData(Rarity.Common, 2)]
        [InlineData(Rarity.Rare, 2)]
        [InlineData(Rarity.Epic, 2)]
        [InlineData(Rarity.Legendary, 1)]
        public void PlaysetSize_is_2_except_legendary(Rarity r, int expected)
        {
            Constants.PlaysetSize(r).Should().Be(expected);
        }

        [Theory]
        [InlineData(Rarity.Common, 400)]
        [InlineData(Rarity.Rare, 800)]
        [InlineData(Rarity.Epic, 1600)]
        [InlineData(Rarity.Legendary, 3200)]
        public void GoldenCraftCost_returns_canonical_values(Rarity r, int expected)
        {
            Constants.GoldenCraftCost(r).Should().Be(expected);
        }
    }
}
