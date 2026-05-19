using System.Collections.Generic;
using System.Linq;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class PatchDiffDetectorTests
    {
        private static CardSnapshot Snap(string id, int? cost = null, int? attack = null, int? health = null, int? durability = null, int? armor = null, string text = null, string name = null)
            => new CardSnapshot(id, name ?? id, cost, attack, health, durability, armor, text);

        [Fact]
        public void Diff_returns_empty_when_no_card_changed()
        {
            var prev = new[] { Snap("C1", cost: 3, attack: 2, health: 4), Snap("C2", cost: 5) };
            var curr = new[] { Snap("C1", cost: 3, attack: 2, health: 4), Snap("C2", cost: 5) };

            var changes = PatchDiffDetector.Diff(prev, curr);

            changes.Should().BeEmpty();
        }

        [Fact]
        public void Diff_ignores_cards_added_or_removed_in_current()
        {
            // Expansion adds new cards — not "changes", they have no previous state.
            // Conversely, removed cards aren't refundable either.
            var prev = new[] { Snap("OLD") };
            var curr = new[] { Snap("OLD"), Snap("NEW") };

            var changes = PatchDiffDetector.Diff(prev, curr);

            changes.Should().BeEmpty();
        }

        [Fact]
        public void Diff_detects_cost_increase_as_nerf()
        {
            var prev = new[] { Snap("CATA_138", cost: 2, name: "Forest's Gift") };
            var curr = new[] { Snap("CATA_138", cost: 3, name: "Forest's Gift") };

            var changes = PatchDiffDetector.Diff(prev, curr);

            changes.Should().ContainSingle();
            changes[0].CardId.Should().Be("CATA_138");
            changes[0].CardName.Should().Be("Forest's Gift");
            changes[0].FieldName.Should().Be("cost");
            changes[0].OldValue.Should().Be("2");
            changes[0].NewValue.Should().Be("3");
            changes[0].Direction.Should().Be(PatchChangeDirection.Nerf);
        }

        [Fact]
        public void Diff_detects_cost_decrease_as_buff()
        {
            var prev = new[] { Snap("MEND_505", cost: 7) };
            var curr = new[] { Snap("MEND_505", cost: 6) };

            var changes = PatchDiffDetector.Diff(prev, curr);

            changes.Should().ContainSingle();
            changes[0].Direction.Should().Be(PatchChangeDirection.Buff);
        }

        [Theory]
        [InlineData("attack", 3, 2, PatchChangeDirection.Nerf)]
        [InlineData("attack", 1, 3, PatchChangeDirection.Buff)]
        [InlineData("health", 5, 3, PatchChangeDirection.Nerf)]
        [InlineData("health", 2, 5, PatchChangeDirection.Buff)]
        [InlineData("durability", 3, 2, PatchChangeDirection.Nerf)]
        [InlineData("durability", 2, 4, PatchChangeDirection.Buff)]
        [InlineData("armor", 5, 3, PatchChangeDirection.Nerf)]
        [InlineData("armor", 3, 5, PatchChangeDirection.Buff)]
        public void Diff_classifies_stat_changes_correctly(string field, int from, int to, PatchChangeDirection expected)
        {
            CardSnapshot p, c;
            switch (field)
            {
                case "attack":     p = Snap("C", attack: from);     c = Snap("C", attack: to); break;
                case "health":     p = Snap("C", health: from);     c = Snap("C", health: to); break;
                case "durability": p = Snap("C", durability: from); c = Snap("C", durability: to); break;
                case "armor":      p = Snap("C", armor: from);      c = Snap("C", armor: to); break;
                default: throw new System.ArgumentException(field);
            }

            var changes = PatchDiffDetector.Diff(new[] { p }, new[] { c });
            changes.Should().ContainSingle();
            changes[0].FieldName.Should().Be(field);
            changes[0].Direction.Should().Be(expected);
        }

        [Fact]
        public void Diff_text_change_is_neutral()
        {
            var prev = new[] { Snap("X", text: "Old text") };
            var curr = new[] { Snap("X", text: "New text") };

            var changes = PatchDiffDetector.Diff(prev, curr);

            changes.Should().ContainSingle();
            changes[0].FieldName.Should().Be("text");
            changes[0].Direction.Should().Be(PatchChangeDirection.Neutral);
        }

        [Fact]
        public void Diff_returns_one_change_per_changed_field()
        {
            // Migrating Elekk in patch 35.4.2: cost 3->2, attack 3->2, health 4->3
            var prev = new[] { Snap("MEND_303", cost: 3, attack: 3, health: 4, name: "Migrating Elekk") };
            var curr = new[] { Snap("MEND_303", cost: 2, attack: 2, health: 3, name: "Migrating Elekk") };

            var changes = PatchDiffDetector.Diff(prev, curr);

            changes.Should().HaveCount(3);
            changes.Select(c => c.FieldName).Should().BeEquivalentTo(new[] { "cost", "attack", "health" });
            // cost 3->2 = Buff, attack 3->2 = Nerf, health 4->3 = Nerf
            changes.Single(c => c.FieldName == "cost").Direction.Should().Be(PatchChangeDirection.Buff);
            changes.Single(c => c.FieldName == "attack").Direction.Should().Be(PatchChangeDirection.Nerf);
            changes.Single(c => c.FieldName == "health").Direction.Should().Be(PatchChangeDirection.Nerf);
        }

        [Fact]
        public void Diff_treats_null_to_value_as_neutral()
        {
            // A card type change (rare) might add a field that was previously null. Don't
            // try to classify direction — flag it as Neutral.
            var prev = new[] { Snap("X", cost: null) };
            var curr = new[] { Snap("X", cost: 5) };

            var changes = PatchDiffDetector.Diff(prev, curr);

            changes.Should().ContainSingle();
            changes[0].Direction.Should().Be(PatchChangeDirection.Neutral);
        }

        [Fact]
        public void Diff_handles_empty_inputs()
        {
            PatchDiffDetector.Diff(System.Array.Empty<CardSnapshot>(), System.Array.Empty<CardSnapshot>())
                .Should().BeEmpty();
        }
    }
}
