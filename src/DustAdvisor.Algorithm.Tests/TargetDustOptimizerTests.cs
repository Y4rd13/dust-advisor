using System.Collections.Generic;
using System.Linq;
using DustAdvisor.Algorithm;
using DustAdvisor.Algorithm.Domain;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Algorithm.Tests
{
    public class TargetDustOptimizerTests
    {
        private static DustItem Item(string id, int dust, bool standardLegal)
            => new DustItem(id, id, Rarity.Common, 1, 0, dust, inRefundWindow: false, isStandardLegal: standardLegal);

        [Fact]
        public void Empty_input_returns_empty()
        {
            var result = TargetDustOptimizer.Optimize(new List<DustItem>(), 500);
            result.Items.Should().BeEmpty();
            result.AchievedDust.Should().Be(0);
        }

        [Fact]
        public void Zero_target_returns_empty()
        {
            var result = TargetDustOptimizer.Optimize(new[] { Item("A", 100, false) }, 0);
            result.Items.Should().BeEmpty();
            result.AchievedDust.Should().Be(0);
        }

        [Fact]
        public void Picks_wild_before_standard_for_same_dust_value()
        {
            var items = new[] { Item("STD", 100, true), Item("WILD", 100, false) };
            var result = TargetDustOptimizer.Optimize(items, 100);
            result.Items.Should().ContainSingle().Which.CardId.Should().Be("WILD");
            result.AchievedDust.Should().Be(100);
        }

        [Fact]
        public void Picks_highest_dust_first_within_format()
        {
            var items = new[] { Item("A", 40, false), Item("B", 400, false), Item("C", 100, false) };
            var result = TargetDustOptimizer.Optimize(items, 450);
            result.Items.Select(i => i.CardId).Should().ContainInOrder("B", "C");
            result.AchievedDust.Should().Be(500);
        }

        [Fact]
        public void Stops_when_target_reached()
        {
            var items = new[] { Item("A", 400, false), Item("B", 400, false), Item("C", 400, false) };
            var result = TargetDustOptimizer.Optimize(items, 500);
            result.Items.Should().HaveCount(2);
            result.AchievedDust.Should().Be(800);
        }

        [Fact]
        public void Returns_all_items_when_target_exceeds_available()
        {
            var items = new[] { Item("A", 100, false), Item("B", 100, false) };
            var result = TargetDustOptimizer.Optimize(items, 1000);
            result.Items.Should().HaveCount(2);
            result.AchievedDust.Should().Be(200);
            result.TargetMet.Should().BeFalse();
        }

        [Fact]
        public void Sets_TargetMet_when_achieved_meets_target()
        {
            var items = new[] { Item("A", 500, false) };
            var result = TargetDustOptimizer.Optimize(items, 500);
            result.TargetMet.Should().BeTrue();
        }
    }
}
