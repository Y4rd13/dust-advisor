using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Data;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class PatchTrackerTests : IDisposable
    {
        private readonly string _tmpDir;
        private readonly string _previousCachePath;
        private readonly string _statePath;
        private readonly string _refundAutoPath;
        private readonly string _url = "https://api.hearthstonejson.com/v1/latest/enUS/cards.collectible.json";

        public PatchTrackerTests()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), $"patchtracker_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tmpDir);
            _previousCachePath = Path.Combine(_tmpDir, "cards.previous.json");
            _statePath = Path.Combine(_tmpDir, "patch_state.json");
            _refundAutoPath = Path.Combine(_tmpDir, "refund_auto.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, recursive: true);
        }

        private sealed class FakeFetcher : IPatchHttpFetcher
        {
            public string LastModified { get; set; }
            public string Body { get; set; }
            public int HeadCalls { get; private set; }
            public int GetCalls { get; private set; }

            public Task<string> HeadLastModifiedAsync(string url, CancellationToken ct)
            {
                HeadCalls++;
                return Task.FromResult(LastModified);
            }

            public Task<(string Body, string LastModified)> GetWithLastModifiedAsync(string url, CancellationToken ct)
            {
                GetCalls++;
                return Task.FromResult((Body, LastModified));
            }
        }

        private static string MakeBuildJson(params (string Id, int? Cost, int? Attack, int? Health, string Name)[] cards)
        {
            var arr = cards.Select(c => new
            {
                id = c.Id,
                dbfId = 0,
                name = c.Name ?? c.Id,
                rarity = "COMMON",
                set = "CORE",
                collectible = true,
                cost = c.Cost,
                attack = c.Attack,
                health = c.Health,
            }).ToList();
            return JsonConvert.SerializeObject(arr);
        }

        private PatchTracker MakeTracker(IPatchHttpFetcher fetcher)
            => new PatchTracker(fetcher, new RefundAutoRepository(),
                _url, _previousCachePath, _statePath, _refundAutoPath, refundWindow: TimeSpan.FromDays(14));

        [Fact]
        public async Task First_run_caches_current_build_without_diffing()
        {
            var fetcher = new FakeFetcher
            {
                LastModified = "Tue, 19 May 2026 17:31:51 GMT",
                Body = MakeBuildJson(("C1", 3, 2, 4, "Card One")),
            };
            var tracker = MakeTracker(fetcher);
            var now = new DateTimeOffset(2026, 5, 19, 17, 32, 0, TimeSpan.Zero);

            var result = await tracker.UpdateAsync(now, CancellationToken.None);

            result.PatchDetected.Should().BeFalse("first run has no previous build to diff against");
            result.ChangedCardCount.Should().Be(0);
            File.Exists(_previousCachePath).Should().BeTrue();
            File.Exists(_statePath).Should().BeTrue();
            File.Exists(_refundAutoPath).Should().BeFalse("no diff = no refund entries written");
            fetcher.GetCalls.Should().Be(1);
        }

        [Fact]
        public async Task Same_last_modified_skips_fetch_and_returns_no_changes()
        {
            // Seed previous cache + state as if we already ran once.
            var lm = "Thu, 07 May 2026 17:19:06 GMT";
            File.WriteAllText(_previousCachePath, MakeBuildJson(("C1", 3, 2, 4, "Card One")));
            File.WriteAllText(_statePath, JsonConvert.SerializeObject(new { lastModified = lm, lastCheckedAt = DateTimeOffset.UtcNow }));

            var fetcher = new FakeFetcher { LastModified = lm };
            var tracker = MakeTracker(fetcher);

            var result = await tracker.UpdateAsync(DateTimeOffset.UtcNow, CancellationToken.None);

            result.PatchDetected.Should().BeFalse();
            result.ChangedCardCount.Should().Be(0);
            fetcher.HeadCalls.Should().Be(1);
            fetcher.GetCalls.Should().Be(0);
        }

        [Fact]
        public async Task New_last_modified_triggers_diff_and_writes_refund_auto()
        {
            // Seed previous = old build with C1 cost=2.
            File.WriteAllText(_previousCachePath, MakeBuildJson(("C1", 2, null, null, "Card One")));
            File.WriteAllText(_statePath, JsonConvert.SerializeObject(new { lastModified = "Thu, 07 May 2026 17:19:06 GMT", lastCheckedAt = DateTimeOffset.UtcNow }));

            // Server reports new build: C1 cost 2→3.
            var newLm = "Tue, 19 May 2026 17:31:51 GMT";
            var fetcher = new FakeFetcher
            {
                LastModified = newLm,
                Body = MakeBuildJson(("C1", 3, null, null, "Card One")),
            };
            var tracker = MakeTracker(fetcher);
            var now = new DateTimeOffset(2026, 5, 19, 17, 32, 0, TimeSpan.Zero);

            var result = await tracker.UpdateAsync(now, CancellationToken.None);

            result.PatchDetected.Should().BeTrue();
            result.ChangedCardCount.Should().Be(1);
            fetcher.GetCalls.Should().Be(1);

            // refund_auto.json should now contain C1 with cost change.
            var repo = new RefundAutoRepository();
            var entries = repo.Load(_refundAutoPath, now).ToList();
            entries.Should().ContainSingle();
            entries[0].CardId.Should().Be("C1");
            entries[0].CardName.Should().Be("Card One");
            entries[0].Changes.Should().ContainSingle();
            entries[0].Changes[0].FieldName.Should().Be("cost");
            entries[0].Changes[0].OldValue.Should().Be("2");
            entries[0].Changes[0].NewValue.Should().Be("3");
            entries[0].Changes[0].Direction.Should().Be(PatchChangeDirection.Nerf);
            // Expiry = patch Last-Modified + 14d
            entries[0].ExpiresUtc.Should().Be(DateTimeOffset.Parse(newLm).AddDays(14));
        }

        [Fact]
        public async Task Diff_groups_multiple_field_changes_per_card_into_one_entry()
        {
            File.WriteAllText(_previousCachePath, MakeBuildJson(("MEND_303", 3, 3, 4, "Migrating Elekk")));
            File.WriteAllText(_statePath, JsonConvert.SerializeObject(new { lastModified = "OLD", lastCheckedAt = DateTimeOffset.UtcNow }));

            var fetcher = new FakeFetcher
            {
                LastModified = "Tue, 19 May 2026 17:31:51 GMT",
                Body = MakeBuildJson(("MEND_303", 2, 2, 3, "Migrating Elekk")),
            };
            var result = await MakeTracker(fetcher).UpdateAsync(new DateTimeOffset(2026, 5, 19, 17, 32, 0, TimeSpan.Zero), CancellationToken.None);

            result.PatchDetected.Should().BeTrue();
            result.ChangedCardCount.Should().Be(1);  // 1 card (3 field changes get grouped)

            var entries = new RefundAutoRepository().Load(_refundAutoPath, DateTimeOffset.UtcNow).ToList();
            entries.Should().ContainSingle();
            entries[0].Changes.Should().HaveCount(3);
        }

        [Fact]
        public async Task Subsequent_run_with_same_last_modified_keeps_existing_refund_entries()
        {
            // Pretend we already detected a patch yesterday — refund_auto.json has one entry.
            var detected = new DateTimeOffset(2026, 5, 18, 17, 31, 51, TimeSpan.Zero);
            var existing = new[]
            {
                new RefundEntry("OLD_PATCH", "Old Card", detected.AddDays(14), detected,
                    new[] { new PatchChange("OLD_PATCH", "Old Card", "cost", "2", "3", PatchChangeDirection.Nerf) })
            };
            new RefundAutoRepository().Save(_refundAutoPath, existing);
            File.WriteAllText(_previousCachePath, MakeBuildJson(("OLD_PATCH", 3, null, null, "Old Card")));
            var lm = "Tue, 19 May 2026 17:31:51 GMT";
            File.WriteAllText(_statePath, JsonConvert.SerializeObject(new { lastModified = lm, lastCheckedAt = DateTimeOffset.UtcNow }));

            var fetcher = new FakeFetcher { LastModified = lm };
            await MakeTracker(fetcher).UpdateAsync(new DateTimeOffset(2026, 5, 19, 17, 32, 0, TimeSpan.Zero), CancellationToken.None);

            // Existing refund entries should be untouched.
            var entries = new RefundAutoRepository().Load(_refundAutoPath, new DateTimeOffset(2026, 5, 19, 17, 32, 0, TimeSpan.Zero)).ToList();
            entries.Should().ContainSingle();
            entries[0].CardId.Should().Be("OLD_PATCH");
        }
    }
}
