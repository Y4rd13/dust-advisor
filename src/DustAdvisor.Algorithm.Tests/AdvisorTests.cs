using System.Collections.Generic;
using System.Linq;
using DustAdvisor.Algorithm;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Algorithm.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Algorithm.Tests
{
    public class AdvisorTests
    {
        [Fact]
        public void Excess_regulars_above_playset_are_safe_to_dust()
        {
            var meta = CardFixtures.CommonWild("EX1_001", 1);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("EX1_001", regular: 5) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            var plan = new Advisor().Recommend(inputs);

            plan.Items.Should().HaveCount(1);
            plan.Items[0].RegularToDust.Should().Be(3);
            plan.Items[0].GoldenToDust.Should().Be(0);
            plan.Items[0].DustGained.Should().Be(15);
            plan.TotalDust.Should().Be(15);
        }

        [Fact]
        public void Legendaries_use_playset_of_one()
        {
            var meta = CardFixtures.LegendaryWild("LEG_001", 100);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("LEG_001", regular: 3) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            var plan = new Advisor().Recommend(inputs);

            plan.Items.Should().ContainSingle()
                .Which.RegularToDust.Should().Be(2);
            plan.TotalDust.Should().Be(800);
        }

        [Fact]
        public void Goldens_count_toward_playset_and_regulars_are_dusted_first()
        {
            // 2 goldens + 2 regulars of a common: playset (2) is satisfied by goldens,
            // so both regulars are safe to dust. No goldens to dust.
            var meta = CardFixtures.CommonWild("EX1_002", 2);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("EX1_002", regular: 2, golden: 2) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            var plan = new Advisor().Recommend(inputs);

            var item = plan.Items.Should().ContainSingle().Subject;
            item.RegularToDust.Should().Be(2);
            item.GoldenToDust.Should().Be(0);
            item.DustGained.Should().Be(10);
        }

        [Fact]
        public void Extra_goldens_beyond_playset_are_safe_to_dust()
        {
            // 3 goldens + 0 regulars of a common: 2 goldens kept (playset), 1 golden dusted (50).
            var meta = CardFixtures.CommonWild("EX1_003", 3);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("EX1_003", regular: 0, golden: 3) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            var plan = new Advisor().Recommend(inputs);

            var item = plan.Items.Should().ContainSingle().Subject;
            item.RegularToDust.Should().Be(0);
            item.GoldenToDust.Should().Be(1);
            item.DustGained.Should().Be(50);
        }

        [Fact]
        public void Core_set_cards_are_skipped()
        {
            var meta = CardFixtures.CoreCard("CORE_001", 9001);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("CORE_001", regular: 5) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            new Advisor().Recommend(inputs).Items.Should().BeEmpty();
        }

        [Fact]
        public void Diamond_copies_are_not_dusted()
        {
            // 1 diamond + 0 others. Diamond is never dustable.
            var meta = CardFixtures.LegendaryWild("LEG_DIAM", 200);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("LEG_DIAM", diamond: 1) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            new Advisor().Recommend(inputs).Items.Should().BeEmpty();
        }

        [Fact]
        public void Diamond_counts_toward_playset_so_regulars_become_dustable()
        {
            // 1 diamond legendary + 1 regular = 2 owned; playset is 1.
            // Diamond holds the playset slot; the 1 regular is safe to dust.
            var meta = CardFixtures.LegendaryWild("LEG_DIAM2", 201);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("LEG_DIAM2", regular: 1, diamond: 1) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            var item = new Advisor().Recommend(inputs).Items.Should().ContainSingle().Subject;
            item.RegularToDust.Should().Be(1);
            item.DustGained.Should().Be(400);
        }

        [Fact]
        public void Locked_golden_copy_is_not_dusted_but_regulars_still_can_be()
        {
            // 1 golden (locked, from Rewards Track) + 3 regulars of a common.
            // Locked golden holds nothing for dust accounting; playset is 2 regulars; 1 regular dustable.
            // The golden is NOT counted toward playset because it is locked (treated as cosmetic-only).
            var meta = CardFixtures.CommonWild("EX1_010", 10);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("EX1_010", regular: 3, golden: 1) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)> { ("EX1_010", Premium.Golden) },
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            var item = new Advisor().Recommend(inputs).Items.Should().ContainSingle().Subject;
            item.RegularToDust.Should().Be(1);
            item.GoldenToDust.Should().Be(0);
            item.DustGained.Should().Be(5);
        }

        [Fact]
        public void Locked_regular_copy_is_not_dusted()
        {
            // 1 regular locked (e.g., Group Learning gift legendary) — never dust it.
            var meta = CardFixtures.LegendaryWild("LEG_LOCK", 300);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("LEG_LOCK", regular: 1) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)> { ("LEG_LOCK", Premium.Regular) },
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            new Advisor().Recommend(inputs).Items.Should().BeEmpty();
        }

        [Fact]
        public void Refund_window_pays_full_craft_cost_per_copy()
        {
            // 3 regular legendaries; 1 kept (playset = 1), 2 dustable.
            // Normal DE = 400 each. Refund window DE = 1600 each.
            // Total expected = 2 * 1600 = 3200.
            var meta = CardFixtures.LegendaryWild("LEG_REFUND", 400);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("LEG_REFUND", regular: 3) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string> { "LEG_REFUND" },
                options: new AdvisorOptions());

            var item = new Advisor().Recommend(inputs).Items.Should().ContainSingle().Subject;
            item.RegularToDust.Should().Be(2);
            item.DustGained.Should().Be(3200);
            item.InRefundWindow.Should().BeTrue();
        }

        [Fact]
        public void Refund_window_pays_full_golden_craft_cost_per_golden_copy()
        {
            // 3 golden commons; playset = 2, 1 dustable.
            // Normal golden DE = 50. Refund golden DE = 400 (full golden craft).
            var meta = CardFixtures.CommonWild("EX1_REF_G", 401);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("EX1_REF_G", golden: 3) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string> { "EX1_REF_G" },
                options: new AdvisorOptions());

            var item = new Advisor().Recommend(inputs).Items.Should().ContainSingle().Subject;
            item.GoldenToDust.Should().Be(1);
            item.DustGained.Should().Be(400);
            item.InRefundWindow.Should().BeTrue();
        }

        [Fact]
        public void SafeOnly_skips_Standard_legal_cards_by_default()
        {
            // Default options: KeepStandardLegal = true, Strategy = SafeOnly.
            // 3 standard-legal rares; nothing should be dusted, and a warning should be emitted.
            var meta = CardFixtures.RareStandard("STD_001", 500);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("STD_001", regular: 3) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions(strategy: Strategy.SafeOnly, keepStandardLegal: true));

            var plan = new Advisor().Recommend(inputs);
            plan.Items.Should().BeEmpty();
            plan.Warnings.Should().ContainSingle(w => w.CardId == "STD_001");
        }

        [Fact]
        public void MaxDust_still_dusts_Standard_legal_cards_but_warns()
        {
            var meta = CardFixtures.RareStandard("STD_002", 501);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("STD_002", regular: 3) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions(strategy: Strategy.MaxDust));

            var plan = new Advisor().Recommend(inputs);
            plan.Items.Should().ContainSingle()
                .Which.IsStandardLegal.Should().BeTrue();
            plan.Warnings.Should().ContainSingle(w => w.CardId == "STD_002");
        }

        [Fact]
        public void RefundOnly_filters_out_cards_not_in_refund_window()
        {
            var refunded = CardFixtures.CommonWild("R_001", 600);
            var notRefunded = CardFixtures.CommonWild("R_002", 601);
            var inputs = new AdvisorInputs(
                collection: new[]
                {
                    new CollectionEntry("R_001", regular: 5),
                    new CollectionEntry("R_002", regular: 5),
                },
                meta: new[] { refunded, notRefunded },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string> { "R_001" },
                options: new AdvisorOptions(strategy: Strategy.RefundOnly));

            var plan = new Advisor().Recommend(inputs);
            plan.Items.Should().ContainSingle().Which.CardId.Should().Be("R_001");
        }

        [Fact]
        public void Plan_is_sorted_refund_first_then_wild_then_by_rarity_desc()
        {
            // 3 cards: a refund-window common, a Wild legendary, and a Standard-legal rare.
            // Default options dust all (use MaxDust to bypass Standard skip).
            var refundCommon = CardFixtures.CommonWild("A", 1);
            var wildLeg = CardFixtures.LegendaryWild("B", 2);
            var stdRare = CardFixtures.RareStandard("C", 3);
            var inputs = new AdvisorInputs(
                collection: new[]
                {
                    new CollectionEntry("A", regular: 5),
                    new CollectionEntry("B", regular: 2),
                    new CollectionEntry("C", regular: 5),
                },
                meta: new[] { refundCommon, wildLeg, stdRare },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string> { "A" },
                options: new AdvisorOptions(strategy: Strategy.MaxDust));

            var plan = new Advisor().Recommend(inputs);
            plan.Items.Select(i => i.CardId).Should().ContainInOrder("A", "B", "C");
        }

        [Fact]
        public void Realistic_mixed_collection_produces_correct_plan()
        {
            // Setup: 5 cards in collection.
            var freeCommon = new CardMeta("FREE_001", 9000, "Free Common", Rarity.Free,
                new CardSet("LEGACY", isStandardLegal: false), isCollectible: true);
            var coreCommon = CardFixtures.CoreCard("CORE_001", 9001);
            var wildRare2 = new CardMeta("WILD_RARE_001", 9003, "Wild Rare", Rarity.Rare,
                new CardSet("OG", isStandardLegal: false), isCollectible: true);
            var wildLeg = CardFixtures.LegendaryWild("WILD_LEG", 9004);
            var refundCommon = CardFixtures.CommonWild("RF_001", 9005);

            var inputs = new AdvisorInputs(
                collection: new[]
                {
                    new CollectionEntry("FREE_001", regular: 10),     // skipped (Free)
                    new CollectionEntry("CORE_001", regular: 10),     // skipped (Core)
                    new CollectionEntry("WILD_RARE_001", regular: 4), // dust 2 @ 20 = 40
                    new CollectionEntry("WILD_LEG", regular: 1, golden: 1), // playset=1; keep golden; dust 1 reg = 400
                    new CollectionEntry("RF_001", regular: 4),        // dust 2 @ 40 (refund) = 80
                },
                meta: new[] { freeCommon, coreCommon, wildRare2, wildLeg, refundCommon },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string> { "RF_001" },
                options: new AdvisorOptions(strategy: Strategy.SafeOnly));

            var plan = new Advisor().Recommend(inputs);
            plan.TotalDust.Should().Be(40 + 400 + 80);
            plan.Items.Should().HaveCount(3);
            // First item must be the refund-window card (RF_001).
            plan.Items[0].CardId.Should().Be("RF_001");
            plan.Items[0].InRefundWindow.Should().BeTrue();
        }

        [Fact]
        public void MaxDust_dusts_goldens_first_when_both_present()
        {
            // 2 regular + 2 golden of a Wild common, playset=2.
            // SafeOnly default: keep goldens, dust 2 regulars → 2*5 = 10 dust.
            // MaxDust: keep regulars, dust 2 goldens → 2*50 = 100 dust.
            var meta = CardFixtures.CommonWild("EX1_MAX_G", 700);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("EX1_MAX_G", regular: 2, golden: 2) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions(strategy: Strategy.MaxDust));

            var item = new Advisor().Recommend(inputs).Items.Should().ContainSingle().Subject;
            item.RegularToDust.Should().Be(0);
            item.GoldenToDust.Should().Be(2);
            item.DustGained.Should().Be(100);
        }

        [Fact]
        public void SafeOnly_still_dusts_regulars_first_when_both_present()
        {
            // Regression: SafeOnly behavior unchanged. 2 reg + 2 gold common, playset=2 → dust 2 reg.
            var meta = CardFixtures.CommonWild("EX1_SAFE_G", 701);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("EX1_SAFE_G", regular: 2, golden: 2) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions(strategy: Strategy.SafeOnly));

            var item = new Advisor().Recommend(inputs).Items.Should().ContainSingle().Subject;
            item.RegularToDust.Should().Be(2);
            item.GoldenToDust.Should().Be(0);
            item.DustGained.Should().Be(10);
        }
    }
}
