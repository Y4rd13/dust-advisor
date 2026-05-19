using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class RefundAutoRepositoryTests : IDisposable
    {
        private readonly string _tmpPath;

        public RefundAutoRepositoryTests()
        {
            _tmpPath = Path.Combine(Path.GetTempPath(), $"refund_auto_{Guid.NewGuid():N}.json");
        }

        public void Dispose()
        {
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
        }

        private static PatchChange CostNerf(string id, string name = null) =>
            new PatchChange(id, name ?? id, "cost", "2", "3", PatchChangeDirection.Nerf);

        private static PatchChange CostBuff(string id, string name = null) =>
            new PatchChange(id, name ?? id, "cost", "5", "4", PatchChangeDirection.Buff);

        private static RefundEntry Entry(string id, DateTimeOffset expires, DateTimeOffset? detected = null, PatchChange[] changes = null) =>
            new RefundEntry(
                id,
                cardName: id,
                expiresUtc: expires,
                detectedAt: detected ?? new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero),
                changes: changes ?? new[] { CostNerf(id) });

        [Fact]
        public void Load_returns_empty_when_file_does_not_exist()
        {
            var repo = new RefundAutoRepository();
            var result = repo.Load(_tmpPath, DateTimeOffset.UtcNow);
            result.Should().BeEmpty();
        }

        [Fact]
        public void Roundtrip_save_then_load_preserves_all_fields()
        {
            var repo = new RefundAutoRepository();
            var detected = new DateTimeOffset(2026, 5, 19, 17, 31, 51, TimeSpan.Zero);
            var expires = detected.AddDays(14);

            var entries = new[]
            {
                new RefundEntry("CATA_138", "Forest's Gift", expires, detected,
                    new[] { new PatchChange("CATA_138", "Forest's Gift", "cost", "2", "3", PatchChangeDirection.Nerf) }),
            };

            repo.Save(_tmpPath, entries);
            var loaded = repo.Load(_tmpPath, now: detected).ToList();

            loaded.Should().ContainSingle();
            loaded[0].CardId.Should().Be("CATA_138");
            loaded[0].CardName.Should().Be("Forest's Gift");
            loaded[0].ExpiresUtc.Should().Be(expires);
            loaded[0].DetectedAt.Should().Be(detected);
            loaded[0].Changes.Should().ContainSingle();
            loaded[0].Changes[0].FieldName.Should().Be("cost");
            loaded[0].Changes[0].OldValue.Should().Be("2");
            loaded[0].Changes[0].NewValue.Should().Be("3");
            loaded[0].Changes[0].Direction.Should().Be(PatchChangeDirection.Nerf);
            loaded[0].AggregateDirection.Should().Be(PatchChangeDirection.Nerf);
        }

        [Fact]
        public void Load_filters_out_expired_entries()
        {
            var repo = new RefundAutoRepository();
            var now = new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero);
            var entries = new[]
            {
                Entry("ACTIVE",  expires: now.AddDays(1)),
                Entry("EXPIRED", expires: now.AddDays(-1)),
                Entry("EDGE",    expires: now),  // exactly equal to now → expired (>=)
            };
            repo.Save(_tmpPath, entries);

            var loaded = repo.Load(_tmpPath, now: now).Select(e => e.CardId).ToList();

            loaded.Should().Contain("ACTIVE");
            loaded.Should().NotContain("EXPIRED");
            loaded.Should().NotContain("EDGE");
        }

        [Fact]
        public void MergeWithNewPatch_drops_expired_and_adds_new_entries()
        {
            var now = new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero);
            var existing = new[]
            {
                Entry("OLD_EXPIRED", expires: now.AddDays(-1)),
                Entry("STILL_VALID", expires: now.AddDays(5)),
            };
            var newChanges = new[]
            {
                CostNerf("NEW_CARD"),
            };
            var patchExpires = now.AddDays(14);

            var merged = RefundAutoRepository.MergeWithNewPatch(existing, newChanges, patchExpires, detectedAt: now, now: now);

            merged.Select(e => e.CardId).Should().BeEquivalentTo(new[] { "STILL_VALID", "NEW_CARD" });
            merged.Single(e => e.CardId == "NEW_CARD").ExpiresUtc.Should().Be(patchExpires);
        }

        [Fact]
        public void MergeWithNewPatch_replaces_existing_entry_when_card_changed_again()
        {
            var now = new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero);
            var firstExpires = now.AddDays(3);   // earlier patch, still valid
            var newPatchExpires = now.AddDays(14);

            var existing = new[]
            {
                Entry("REPEAT", expires: firstExpires,
                    changes: new[] { CostBuff("REPEAT") }),  // first patch was a buff
            };
            var newChanges = new[]
            {
                CostNerf("REPEAT"),  // second patch nerfs the same card
            };

            var merged = RefundAutoRepository.MergeWithNewPatch(existing, newChanges, newPatchExpires, detectedAt: now, now: now);

            merged.Should().ContainSingle();
            var entry = merged[0];
            entry.CardId.Should().Be("REPEAT");
            // newer patch's expiry wins
            entry.ExpiresUtc.Should().Be(newPatchExpires);
            // newer patch's changes (Nerf) replace the buff
            entry.AggregateDirection.Should().Be(PatchChangeDirection.Nerf);
            entry.Changes.Should().ContainSingle();
            entry.Changes[0].Direction.Should().Be(PatchChangeDirection.Nerf);
        }

        [Fact]
        public void MergeWithNewPatch_groups_multiple_changes_to_same_card_into_one_entry()
        {
            // A patch can change multiple fields on one card (cost + attack + health).
            // Merge should produce ONE entry per card with all the field changes.
            var now = new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero);
            var changes = new[]
            {
                new PatchChange("MEND_303", "Migrating Elekk", "cost",   "3", "2", PatchChangeDirection.Buff),
                new PatchChange("MEND_303", "Migrating Elekk", "attack", "3", "2", PatchChangeDirection.Nerf),
                new PatchChange("MEND_303", "Migrating Elekk", "health", "4", "3", PatchChangeDirection.Nerf),
            };
            var merged = RefundAutoRepository.MergeWithNewPatch(System.Array.Empty<RefundEntry>(), changes, now.AddDays(14), detectedAt: now, now: now);

            merged.Should().ContainSingle();
            var entry = merged[0];
            entry.CardId.Should().Be("MEND_303");
            entry.CardName.Should().Be("Migrating Elekk");
            entry.Changes.Should().HaveCount(3);
            entry.Changes.Select(c => c.FieldName).Should().BeEquivalentTo(new[] { "cost", "attack", "health" });
            // Any nerf component → aggregate Nerf
            entry.AggregateDirection.Should().Be(PatchChangeDirection.Nerf);
        }
    }
}
