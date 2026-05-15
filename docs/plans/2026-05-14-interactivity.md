# Dust Advisor v1.1 Interactivity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three packages of interactivity to the existing HDT dust-advisor plugin — visual card-art preview & keyboard shortcuts (A), selection cart + undo + toasts (B), persistent "never suggest" list + target-dust optimizer (C).

**Architecture:** Net new data classes for `NeverSuggestRepository`, `TargetDustOptimizer`, `CardArtCache`, `SessionLedger`, `UndoStack`. WPF changes confined to `DustAdvisorWindow.xaml` + xaml.cs and a new `CardDetailWindow.xaml`. The Advisor algorithm is untouched — never-suggest entries flow into the existing `Uncraftable` set, target-dust filtering happens in the UI layer.

**Tech Stack:** Same as v1.0 (C# 7.3, .NET Standard 2.0 for algorithm/data, .NET Framework 4.8 + WPF for UI/HDT plugin, xUnit + FluentAssertions, Newtonsoft.Json). HearthstoneJSON CDN for card art.

**Reference spec:** `docs/specs/2026-05-14-interactivity-design.md`.

**Environment:** WSL Linux. Build with `'/mnt/c/Program Files/dotnet/dotnet.exe'` + `wslpath -w` for paths. Working dir: `/mnt/g/Documentos/Projects/hearthstone`.

**Branch:** `feat/dust-advisor-mvp` (continue on the same branch as v1.0).

---

## Task 1: NeverSuggestRepository — load only (TDD)

**Files:**
- Create: `src/DustAdvisor.Data/NeverSuggestRepository.cs`
- Create: `src/DustAdvisor.Data.Tests/Fixtures/never_suggest.json`
- Create: `src/DustAdvisor.Data.Tests/NeverSuggestRepositoryTests.cs`
- Modify: `src/DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj` (copy new fixture)

- [ ] **Step 1: Add fixture**

Create `src/DustAdvisor.Data.Tests/Fixtures/never_suggest.json`:

```json
[
  { "cardId": "EX1_565", "premium": "Regular" },
  { "cardId": "CORE_AT_001", "premium": "Golden" }
]
```

Edit `src/DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj`. Find the existing `<ItemGroup>` with `<None Update="Fixtures\...">` entries and append a new one for `never_suggest.json`:

```xml
  <None Update="Fixtures\never_suggest.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
```

- [ ] **Step 2: Write the failing test**

Create `src/DustAdvisor.Data.Tests/NeverSuggestRepositoryTests.cs`:

```csharp
using System;
using System.IO;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class NeverSuggestRepositoryTests
    {
        [Fact]
        public void Load_returns_keyed_tuples_for_all_entries()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "never_suggest.json");
            var repo = new NeverSuggestRepository();
            var set = repo.Load(path);

            set.Should().Contain(("EX1_565", Premium.Regular));
            set.Should().Contain(("CORE_AT_001", Premium.Golden));
            set.Should().HaveCount(2);
        }

        [Fact]
        public void Load_returns_empty_when_file_missing()
        {
            var repo = new NeverSuggestRepository();
            var set = repo.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".json"));
            set.Should().BeEmpty();
        }
    }
}
```

- [ ] **Step 3: Run — fails**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj)" 2>&1 | tail -10
```

Expected: build failure `NeverSuggestRepository not found`.

- [ ] **Step 4: Implement the repository (load only for now)**

Create `src/DustAdvisor.Data/NeverSuggestRepository.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using DustAdvisor.Algorithm.Domain;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class NeverSuggestRepository
    {
        private sealed class Entry
        {
            [JsonProperty("cardId")] public string CardId { get; set; }
            [JsonProperty("premium")] public string Premium { get; set; }
        }

        public IReadOnlyCollection<(string CardId, Premium Premium)> Load(string path)
        {
            if (!File.Exists(path)) return new HashSet<(string, Premium)>();
            var raw = File.ReadAllText(path);
            var entries = JsonConvert.DeserializeObject<List<Entry>>(raw) ?? new List<Entry>();
            var set = new HashSet<(string, Premium)>();
            foreach (var e in entries)
            {
                if (System.Enum.TryParse<Premium>(e.Premium, ignoreCase: true, out var p))
                    set.Add((e.CardId, p));
            }
            return set;
        }
    }
}
```

- [ ] **Step 5: Run — passes**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj)" 2>&1 | tail -5
```

Expected: all data tests pass (currently 19 + 2 new = 21).

- [ ] **Step 6: Commit**

```bash
cd /mnt/g/Documentos/Projects/hearthstone
git add src/DustAdvisor.Data/NeverSuggestRepository.cs src/DustAdvisor.Data.Tests/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(data): NeverSuggestRepository.Load reads per-card uncraftable overrides"
```

---

## Task 2: NeverSuggestRepository.Save (atomic write, debounced)

**Files:**
- Modify: `src/DustAdvisor.Data/NeverSuggestRepository.cs`
- Modify: `src/DustAdvisor.Data.Tests/NeverSuggestRepositoryTests.cs`

- [ ] **Step 1: Add failing test**

Append to `NeverSuggestRepositoryTests.cs`:

```csharp
        [Fact]
        public void Save_then_Load_roundtrips()
        {
            var path = Path.Combine(Path.GetTempPath(), "never_suggest_test_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var repo = new NeverSuggestRepository();
                var entries = new[]
                {
                    ("EX1_001", Premium.Regular),
                    ("EX1_002", Premium.Golden),
                };
                repo.Save(path, entries);

                var loaded = repo.Load(path);
                loaded.Should().Contain(("EX1_001", Premium.Regular));
                loaded.Should().Contain(("EX1_002", Premium.Golden));
                loaded.Should().HaveCount(2);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Save_writes_atomically_via_temp_file()
        {
            var path = Path.Combine(Path.GetTempPath(), "never_suggest_atomic_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "[{\"cardId\":\"OLD\",\"premium\":\"Regular\"}]");
                var repo = new NeverSuggestRepository();
                repo.Save(path, new[] { ("NEW", Premium.Golden) });

                var loaded = repo.Load(path);
                loaded.Should().ContainSingle().Which.Should().Be(("NEW", Premium.Golden));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
```

- [ ] **Step 2: Run — fails (Save method missing)**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj)" 2>&1 | tail -10
```

Expected: build error referring to missing `Save`.

- [ ] **Step 3: Implement Save (atomic temp+rename)**

In `src/DustAdvisor.Data/NeverSuggestRepository.cs`, add this method inside the class:

```csharp
        public void Save(string path, System.Collections.Generic.IEnumerable<(string CardId, Premium Premium)> entries)
        {
            var list = new List<Entry>();
            foreach (var (cardId, premium) in entries)
                list.Add(new Entry { CardId = cardId, Premium = premium.ToString() });

            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
```

- [ ] **Step 4: Run — passes**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj)" 2>&1 | tail -5
```

Expected: 23 data tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Data/NeverSuggestRepository.cs src/DustAdvisor.Data.Tests/NeverSuggestRepositoryTests.cs
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(data): NeverSuggestRepository.Save with atomic temp+rename"
```

---

## Task 3: DataLoader merges never-suggest into uncraftable set

**Files:**
- Modify: `src/DustAdvisor.Data/DataLoader.cs`
- Modify: `src/DustAdvisor.Data.Tests/DataLoaderTests.cs`
- Modify: `src/DustAdvisor.Data.Tests/Fixtures/never_suggest.json` (already created in Task 1, reuse)

- [ ] **Step 1: Update DataLoader API**

In `src/DustAdvisor.Data/DataLoader.cs`, replace the file with:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Data
{
    public sealed class DataLoader
    {
        private readonly IHearthstoneJsonClient _hsj;
        private readonly UncraftableRepository _uncraftableRepo;
        private readonly RefundRepository _refundRepo;
        private readonly NeverSuggestRepository _neverSuggestRepo;

        public DataLoader(IHearthstoneJsonClient hsj, UncraftableRepository uncraftableRepo, RefundRepository refundRepo, NeverSuggestRepository neverSuggestRepo)
        {
            _hsj = hsj;
            _uncraftableRepo = uncraftableRepo;
            _refundRepo = refundRepo;
            _neverSuggestRepo = neverSuggestRepo;
        }

        public async Task<DataSnapshot> LoadAsync(
            string locale,
            string uncraftablePath,
            string refundPath,
            string neverSuggestPath,
            DateTimeOffset now,
            CancellationToken ct)
        {
            var meta = await _hsj.LoadCollectibleAsync(locale, ct).ConfigureAwait(false);
            var heuristic = await _hsj.LoadHeuristicUncraftableAsync(locale, ct).ConfigureAwait(false);
            var curated = _uncraftableRepo.Load(uncraftablePath);
            var neverSuggest = _neverSuggestRepo.Load(neverSuggestPath);

            var merged = new HashSet<(string CardId, Premium Premium)>();
            foreach (var c in curated) merged.Add(c);
            foreach (var h in heuristic) merged.Add(h);
            foreach (var n in neverSuggest) merged.Add(n);

            var refund = _refundRepo.Load(refundPath, now);
            return new DataSnapshot(meta, merged, refund);
        }
    }
}
```

- [ ] **Step 2: Update DataLoaderTests to use the new signature**

Open `src/DustAdvisor.Data.Tests/DataLoaderTests.cs`. The existing test calls `new DataLoader(...)` with 3 args. Update it to pass 4 args and to also pass a `neverSuggestPath` to `LoadAsync`. Replace the test body with:

```csharp
        [Fact]
        public async Task LoadAsync_composes_meta_uncraftable_refund_and_never_suggest()
        {
            var cardsPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_cards.json");
            var unPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "uncraftable.json");
            var refundPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "refund.json");
            var neverPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "never_suggest.json");

            var loader = new DataLoader(
                new HearthstoneJsonClient(new HearthstoneJsonClientTests.LocalFileFetcherPublic(cardsPath)),
                new UncraftableRepository(),
                new RefundRepository(),
                new NeverSuggestRepository());

            var snapshot = await loader.LoadAsync(
                locale: "enUS",
                uncraftablePath: unPath,
                refundPath: refundPath,
                neverSuggestPath: neverPath,
                now: new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero),
                ct: CancellationToken.None);

            snapshot.Meta.Should().HaveCount(3);
            // Uncraftable now merges: 2 curated + 2 never-suggest = 4 total (assuming no overlap)
            snapshot.Uncraftable.Should().HaveCount(4);
            snapshot.RefundWindow.Should().ContainSingle().Which.Should().Be("NERFED_001");
        }
```

- [ ] **Step 3: Run — passes after fixing call site**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj)" 2>&1 | tail -5
```

Expected: all data tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/DustAdvisor.Data/DataLoader.cs src/DustAdvisor.Data.Tests/DataLoaderTests.cs
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(data): DataLoader merges never-suggest into the uncraftable set"
```

---

## Task 4: Wire never-suggest path into the HDT plugin

**Files:**
- Modify: `src/DustAdvisor.Hdt/PluginPaths.cs`
- Modify: `src/DustAdvisor.Hdt/Plugin.cs`

- [ ] **Step 1: Add path to PluginPaths**

In `src/DustAdvisor.Hdt/PluginPaths.cs`, append inside the class (after `RefundFile`):

```csharp
        public static string NeverSuggestFile => Path.Combine(PluginRoot, "data", "never_suggest.json");
        public static string CardArtDir => Path.Combine(PluginRoot, "cache", "art");
```

- [ ] **Step 2: Update Plugin.RunAsync**

In `src/DustAdvisor.Hdt/Plugin.cs`, find the `new DustAdvisor.Data.DataLoader(...)` construction. Update it to pass a `NeverSuggestRepository`:

Find:
```csharp
                var loader = new DustAdvisor.Data.DataLoader(
                    hsj,
                    new DustAdvisor.Data.UncraftableRepository(),
                    new DustAdvisor.Data.RefundRepository());
```

Replace with:
```csharp
                var loader = new DustAdvisor.Data.DataLoader(
                    hsj,
                    new DustAdvisor.Data.UncraftableRepository(),
                    new DustAdvisor.Data.RefundRepository(),
                    new DustAdvisor.Data.NeverSuggestRepository());
```

Then find the `var data = await loader.LoadAsync(...)` call and add the `neverSuggestPath` argument. Final shape:

```csharp
                var data = await loader.LoadAsync(
                    locale: "enUS",
                    uncraftablePath: PluginPaths.UncraftableFile,
                    refundPath: PluginPaths.RefundFile,
                    neverSuggestPath: PluginPaths.NeverSuggestFile,
                    now: System.DateTimeOffset.UtcNow,
                    ct: System.Threading.CancellationToken.None);
```

- [ ] **Step 3: Build**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: `Compilación correcta`, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/DustAdvisor.Hdt/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(hdt): pass never-suggest path into DataLoader"
```

---

## Task 5: TargetDustOptimizer (pure function, TDD)

**Files:**
- Create: `src/DustAdvisor.Algorithm/TargetDustOptimizer.cs`
- Create: `src/DustAdvisor.Algorithm.Tests/TargetDustOptimizerTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/DustAdvisor.Algorithm.Tests/TargetDustOptimizerTests.cs`:

```csharp
using System.Collections.Generic;
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
```

Note: this file uses `Select` from LINQ. Add `using System.Linq;` at the top.

- [ ] **Step 2: Run — fails (TargetDustOptimizer doesn't exist)**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Algorithm.Tests/DustAdvisor.Algorithm.Tests.csproj)" 2>&1 | tail -10
```

Expected: build error `TargetDustOptimizer not found`.

- [ ] **Step 3: Implement**

Create `src/DustAdvisor.Algorithm/TargetDustOptimizer.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm
{
    public sealed class TargetDustOptimizerResult
    {
        public IReadOnlyList<DustItem> Items { get; }
        public int AchievedDust { get; }
        public int Target { get; }
        public bool TargetMet => AchievedDust >= Target && Target > 0;

        public TargetDustOptimizerResult(IReadOnlyList<DustItem> items, int achievedDust, int target)
        {
            Items = items;
            AchievedDust = achievedDust;
            Target = target;
        }
    }

    public static class TargetDustOptimizer
    {
        public static TargetDustOptimizerResult Optimize(IEnumerable<DustItem> items, int target)
        {
            if (target <= 0)
                return new TargetDustOptimizerResult(new List<DustItem>(), 0, target);

            // Sort by IsStandardLegal asc (false before true → Wild first), then DustGained desc (biggest first).
            var sorted = items
                .OrderBy(i => i.IsStandardLegal)
                .ThenByDescending(i => i.DustGained)
                .ToList();

            var picked = new List<DustItem>();
            int total = 0;
            foreach (var i in sorted)
            {
                if (total >= target) break;
                picked.Add(i);
                total += i.DustGained;
            }
            return new TargetDustOptimizerResult(picked, total, target);
        }
    }
}
```

- [ ] **Step 4: Run — passes**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Algorithm.Tests/DustAdvisor.Algorithm.Tests.csproj)" 2>&1 | tail -5
```

Expected: 36 + 7 new = 43 algorithm tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Algorithm/TargetDustOptimizer.cs src/DustAdvisor.Algorithm.Tests/TargetDustOptimizerTests.cs
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(algorithm): TargetDustOptimizer greedy subset (Wild before Standard, big dust first)"
```

---

## Task 6: Target dust input in the WPF window

**Files:**
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`

- [ ] **Step 1: Add the input to the header XAML**

Open `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`. Find the first `StackPanel` (the one in `Grid.Row="0"`). After the `KeepStandardBox` element, append:

```xml
      <TextBlock Text="Target dust:" VerticalAlignment="Center" Margin="16,0,4,0"/>
      <TextBox x:Name="TargetDustBox" Width="80" VerticalAlignment="Center"/>
      <TextBlock x:Name="TargetStatusLabel" VerticalAlignment="Center" Margin="8,0,0,0" Foreground="Gray"/>
```

- [ ] **Step 2: Wire the input**

In `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`, find the constructor. Inside the constructor, after the existing event wiring for `ShowNormalBox`/`ShowGoldenBox`, add:

```csharp
            TargetDustBox.TextChanged += (s, e) => Render(_plan);
```

Then update the `Render` method. Currently it computes `visible` from `ApplyFilters`. Replace the Render method body with:

```csharp
        public void Render(DustPlan plan)
        {
            if (!_ready) return;
            var filtered = ApplyFilters(plan.Items).ToList();

            IReadOnlyList<DustItem> visible;
            int? target = null;
            if (int.TryParse(TargetDustBox.Text, out int t) && t > 0)
            {
                target = t;
                var optimized = DustAdvisor.Algorithm.TargetDustOptimizer.Optimize(filtered, t);
                visible = optimized.Items;
                TargetStatusLabel.Text = optimized.TargetMet
                    ? $"{optimized.AchievedDust:n0} / {t:n0} target ({visible.Count} cards)"
                    : $"{optimized.AchievedDust:n0} / {t:n0} target — not enough safe dust";
                TargetStatusLabel.Foreground = optimized.TargetMet
                    ? System.Windows.Media.Brushes.DarkGreen
                    : System.Windows.Media.Brushes.OrangeRed;
            }
            else
            {
                visible = filtered;
                TargetStatusLabel.Text = string.Empty;
            }

            var visibleDust = visible.Sum(i => i.DustGained);
            TotalDustLabel.Text = (target.HasValue || visible.Count != plan.Items.Count)
                ? $"{visibleDust:n0} dust  (of {plan.TotalDust:n0} unfiltered)"
                : $"{plan.TotalDust:n0} dust";
            WarningCountLabel.Text = plan.Warnings.Count > 0 ? $"{plan.Warnings.Count} warning(s)" : string.Empty;
            ItemsGrid.ItemsSource = visible.Select(i => new DustItemRow(i)).ToList();
        }
```

- [ ] **Step 3: Build**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/DustAdvisor.Ui/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): target-dust input filters plan to minimum-pain subset"
```

---

## Task 7: CardArtCache — HTTP fetch + disk cache (TDD)

**Files:**
- Create: `src/DustAdvisor.Ui.Export/CardArtCache.cs` (kept in Ui.Export because it's netstandard2.0 + testable cross-platform)
- Create: `src/DustAdvisor.Ui.Tests/CardArtCacheTests.cs`

Note: we put `CardArtCache` in `DustAdvisor.Ui.Export` (netstandard2.0) so we can unit-test it on net8.0. It has no WPF dependency — it just downloads and caches bytes.

- [ ] **Step 1: Write failing test**

Create `src/DustAdvisor.Ui.Tests/CardArtCacheTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Ui.Export;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Ui.Tests
{
    public class CardArtCacheTests
    {
        [Fact]
        public async Task GetAsync_downloads_and_caches_on_first_hit()
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "art_cache_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var stub = new StubFetcher();
                stub.NextBytes = new byte[] { 1, 2, 3, 4 };
                var cache = new CardArtCache(stub, cacheDir);

                var first = await cache.GetAsync("EX1_001", size: 256, ct: CancellationToken.None);
                first.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });
                stub.CallCount.Should().Be(1);

                stub.NextBytes = new byte[] { 9, 9, 9, 9 };
                var second = await cache.GetAsync("EX1_001", size: 256, ct: CancellationToken.None);
                second.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });
                stub.CallCount.Should().Be(1); // served from disk cache
            }
            finally
            {
                if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
            }
        }

        [Fact]
        public async Task GetAsync_returns_null_when_fetcher_fails_and_no_cache()
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "art_cache_fail_" + Guid.NewGuid().ToString("N"));
            try
            {
                var stub = new StubFetcher { ShouldThrow = true };
                var cache = new CardArtCache(stub, cacheDir);

                var result = await cache.GetAsync("EX1_999", size: 256, ct: CancellationToken.None);
                result.Should().BeNull();
            }
            finally
            {
                if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
            }
        }

        private sealed class StubFetcher : IBinaryFetcher
        {
            public byte[] NextBytes { get; set; } = new byte[0];
            public bool ShouldThrow { get; set; }
            public int CallCount { get; private set; }
            public Task<byte[]> GetBytesAsync(string url, CancellationToken ct)
            {
                CallCount++;
                if (ShouldThrow) throw new System.Net.Http.HttpRequestException("no network");
                return Task.FromResult(NextBytes);
            }
        }
    }
}
```

- [ ] **Step 2: Run — fails (CardArtCache + IBinaryFetcher don't exist)**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Ui.Tests/DustAdvisor.Ui.Tests.csproj)" 2>&1 | tail -10
```

- [ ] **Step 3: Implement**

Create `src/DustAdvisor.Ui.Export/CardArtCache.cs`:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Ui.Export
{
    public interface IBinaryFetcher
    {
        Task<byte[]> GetBytesAsync(string url, CancellationToken ct);
    }

    public sealed class CardArtCache
    {
        private const string BaseUrl = "https://art.hearthstonejson.com/v1/render/latest/enUS";

        private readonly IBinaryFetcher _fetcher;
        private readonly string _cacheDir;

        public CardArtCache(IBinaryFetcher fetcher, string cacheDir)
        {
            _fetcher = fetcher;
            _cacheDir = cacheDir;
            Directory.CreateDirectory(_cacheDir);
        }

        public async Task<byte[]> GetAsync(string cardId, int size, CancellationToken ct)
        {
            var path = Path.Combine(_cacheDir, $"{cardId}_{size}.png");
            if (File.Exists(path)) return File.ReadAllBytes(path);

            var url = $"{BaseUrl}/{size}x/{cardId}.png";
            try
            {
                var bytes = await _fetcher.GetBytesAsync(url, ct).ConfigureAwait(false);
                File.WriteAllBytes(path, bytes);
                return bytes;
            }
            catch
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 4: Run — passes**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Ui.Tests/DustAdvisor.Ui.Tests.csproj)" 2>&1 | tail -5
```

Expected: 1 + 2 new = 3 Ui tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Ui.Export/CardArtCache.cs src/DustAdvisor.Ui.Tests/CardArtCacheTests.cs
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): CardArtCache fetches HearthstoneJSON renders with disk cache"
```

---

## Task 8: Production HttpBinaryFetcher + hover preview ToolTip

**Files:**
- Create: `src/DustAdvisor.Ui/HttpBinaryFetcher.cs` (production wrapping HttpClient)
- Modify: `src/DustAdvisor.Ui/DustItemRow.cs` (add CardId already present; add image fetch helper)
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml` (add ToolTip template)
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs` (instantiate CardArtCache, async load on hover)

- [ ] **Step 1: Create HttpBinaryFetcher**

Create `src/DustAdvisor.Ui/HttpBinaryFetcher.cs`:

```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Ui.Export;

namespace DustAdvisor.Ui
{
    public sealed class HttpBinaryFetcher : IBinaryFetcher
    {
        private static readonly HttpClient SharedClient = new HttpClient();

        public async Task<byte[]> GetBytesAsync(string url, CancellationToken ct)
        {
            using (var resp = await SharedClient.GetAsync(url, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }
    }
}
```

- [ ] **Step 2: Update DustItemRow to expose the image source**

Open `src/DustAdvisor.Ui/DustItemRow.cs`. The existing class has `CardId` already. Add a `BitmapImage` property that the XAML can bind to, plus a method to populate it asynchronously. Replace the file with:

```csharp
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Ui.Export;

namespace DustAdvisor.Ui
{
    public sealed class DustItemRow : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public string CardId { get; }
        public string Name { get; }
        public string Rarity { get; }
        public int RegularToDust { get; }
        public int GoldenToDust { get; }
        public int DustGained { get; }
        public string Flag { get; }

        private BitmapImage _art;
        public BitmapImage Art
        {
            get { return _art; }
            private set { _art = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Art))); }
        }

        public DustItemRow(DustItem item)
        {
            CardId = item.CardId;
            Name = item.CardName;
            Rarity = item.Rarity.ToString();
            RegularToDust = item.RegularToDust;
            GoldenToDust = item.GoldenToDust;
            DustGained = item.DustGained;
            Flag = item.InRefundWindow ? "REFUND"
                 : item.IsStandardLegal ? "STANDARD"
                 : "WILD";
        }

        public async Task EnsureArtLoadedAsync(CardArtCache cache, int size = 256)
        {
            if (Art != null) return;
            var bytes = await cache.GetAsync(CardId, size, System.Threading.CancellationToken.None).ConfigureAwait(false);
            if (bytes == null) return;

            // Decode on a background thread; assign on UI thread.
            BitmapImage bmp = null;
            await Task.Run(() =>
            {
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
            });
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Art = bmp);
        }
    }
}
```

- [ ] **Step 3: Add ToolTip template to the DataGrid XAML**

Open `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`. Find the `<DataGrid x:Name="ItemsGrid" ...>` element. Add the `RowStyle` and `ToolTipService` configuration. Replace the whole `<DataGrid>` block with:

```xml
    <DataGrid x:Name="ItemsGrid" Grid.Row="2" Margin="8" AutoGenerateColumns="False" IsReadOnly="True"
              ToolTipService.InitialShowDelay="300" ToolTipService.ShowDuration="20000">
      <DataGrid.RowStyle>
        <Style TargetType="DataGridRow">
          <Setter Property="ToolTip">
            <Setter.Value>
              <ToolTip Background="Transparent" BorderThickness="0" Placement="Mouse">
                <Image Source="{Binding Art}" Width="260" Height="380" Stretch="Uniform"/>
              </ToolTip>
            </Setter.Value>
          </Setter>
          <EventSetter Event="MouseEnter" Handler="DataGridRow_MouseEnter"/>
        </Style>
      </DataGrid.RowStyle>
      <DataGrid.Columns>
        <DataGridTextColumn Header="Card" Binding="{Binding Name}" Width="220"/>
        <DataGridTextColumn Header="Rarity" Binding="{Binding Rarity}" Width="90"/>
        <DataGridTextColumn Header="Reg" Binding="{Binding RegularToDust}" Width="50"/>
        <DataGridTextColumn Header="Gold" Binding="{Binding GoldenToDust}" Width="55"/>
        <DataGridTextColumn Header="Dust" Binding="{Binding DustGained}" Width="80"/>
        <DataGridTextColumn Header="Flag" Binding="{Binding Flag}" Width="100"/>
      </DataGrid.Columns>
    </DataGrid>
```

- [ ] **Step 4: Add MouseEnter handler in code-behind**

Open `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`. At the top of the file, add:

```csharp
using DustAdvisor.Ui.Export;
```

Add a private field for the cache, initialize it in the constructor, and add the handler. After the `_recompute` field declaration, add:

```csharp
        private readonly CardArtCache _artCache;
```

Modify the constructor signature to accept a cache:

```csharp
        public DustAdvisorWindow(DustPlan plan, Func<AdvisorOptions, DustPlan> recompute, CardArtCache artCache)
        {
            InitializeComponent();
            _plan = plan;
            _recompute = recompute;
            _artCache = artCache;
            // ... existing init code unchanged
```

At the bottom of the class, add:

```csharp
        private async void DataGridRow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGridRow row && row.DataContext is DustItemRow item)
            {
                await item.EnsureArtLoadedAsync(_artCache);
            }
        }
```

- [ ] **Step 5: Update Plugin.RunAsync to construct the cache and pass it to the window**

Open `src/DustAdvisor.Hdt/Plugin.cs`. Find the window construction `var win = new DustAdvisor.Ui.DustAdvisorWindow(initialPlan, recompute);`. Before it, add:

```csharp
                var artCache = new DustAdvisor.Ui.Export.CardArtCache(
                    new DustAdvisor.Ui.HttpBinaryFetcher(),
                    PluginPaths.CardArtDir);
```

And update the construction:

```csharp
                var win = new DustAdvisor.Ui.DustAdvisorWindow(initialPlan, recompute, artCache);
```

- [ ] **Step 6: Build**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/DustAdvisor.Ui/ src/DustAdvisor.Hdt/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): hover preview shows card art via HearthstoneJSON CDN with disk cache"
```

---

## Task 9: CardDetailWindow (double-click for big art)

**Files:**
- Create: `src/DustAdvisor.Ui/CardDetailWindow.xaml`
- Create: `src/DustAdvisor.Ui/CardDetailWindow.xaml.cs`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml` (wire double-click)
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs` (handler)

- [ ] **Step 1: Create the detail window XAML**

Create `src/DustAdvisor.Ui/CardDetailWindow.xaml`:

```xml
<Window x:Class="DustAdvisor.Ui.CardDetailWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Card" Height="640" Width="440" WindowStartupLocation="CenterOwner">
  <Grid Margin="12">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    <TextBlock x:Name="HeaderText" FontSize="16" FontWeight="Bold" Margin="0,0,0,8"/>
    <Image x:Name="ArtImage" Grid.Row="1" Stretch="Uniform"/>
    <TextBlock x:Name="DetailsText" Grid.Row="2" Margin="0,8,0,0" TextWrapping="Wrap"/>
  </Grid>
</Window>
```

Create `src/DustAdvisor.Ui/CardDetailWindow.xaml.cs`:

```csharp
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Ui.Export;

namespace DustAdvisor.Ui
{
    public partial class CardDetailWindow : Window
    {
        public CardDetailWindow(DustItem item, CardArtCache cache)
        {
            InitializeComponent();
            Title = item.CardName;
            HeaderText.Text = $"{item.CardName} — {item.Rarity}";

            var flag = item.InRefundWindow ? "REFUND" : item.IsStandardLegal ? "STANDARD-LEGAL" : "WILD";
            DetailsText.Text =
                $"Regulars to dust: {item.RegularToDust}\n" +
                $"Goldens to dust:  {item.GoldenToDust}\n" +
                $"Dust gained:      {item.DustGained:n0}\n" +
                $"Format:           {flag}\n" +
                $"Card id:          {item.CardId}";

            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            Loaded += async (s, e) =>
            {
                var bytes = await cache.GetAsync(item.CardId, size: 512, ct: CancellationToken.None);
                if (bytes == null) return;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
                ArtImage.Source = bmp;
            };
        }
    }
}
```

- [ ] **Step 2: Wire double-click on the grid**

Open `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`. In the `<DataGrid x:Name="ItemsGrid" ...>` element, find the `RowStyle` block. Inside the `<Style TargetType="DataGridRow">` add an event setter for double-click:

```xml
          <EventSetter Event="MouseDoubleClick" Handler="DataGridRow_MouseDoubleClick"/>
```

Place it next to the existing `<EventSetter Event="MouseEnter" .../>`.

- [ ] **Step 3: Handle double-click**

In `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`, at the bottom of the class, add:

```csharp
        private void DataGridRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGridRow row && row.DataContext is DustItemRow uirow)
            {
                // Find the matching DustItem (the row VM doesn't keep a reference to it).
                var item = _plan.Items.FirstOrDefault(x => x.CardId == uirow.CardId);
                if (item == null) return;
                var win = new CardDetailWindow(item, _artCache) { Owner = this };
                win.Show();
            }
        }
```

- [ ] **Step 4: Build**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Ui/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): CardDetailWindow opens on row double-click (512x art + dust math)"
```

---

## Task 10: Selection cart — checkbox column + IsSelected state

**Files:**
- Modify: `src/DustAdvisor.Ui/DustItemRow.cs`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`

- [ ] **Step 1: Add IsSelected to the row VM**

In `src/DustAdvisor.Ui/DustItemRow.cs`, add an `IsSelected` field with `INotifyPropertyChanged`. Inside the class, before the constructor, add:

```csharp
        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); }
        }
```

- [ ] **Step 2: Add checkbox column to the grid**

Open `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`. In the `<DataGrid.Columns>` block, **prepend** a checkbox column as the first child:

```xml
        <DataGridCheckBoxColumn Header="" Binding="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}" Width="32"/>
```

(Place this BEFORE the existing `<DataGridTextColumn Header="Card" ...>`.)

- [ ] **Step 3: Build**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: 0 errors. Checkboxes appear in the leftmost column; toggling them updates `IsSelected` but nothing else changes visually yet.

- [ ] **Step 4: Commit**

```bash
git add src/DustAdvisor.Ui/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): per-row IsSelected checkbox column for selection cart"
```

---

## Task 11: Sticky confirm bar

**Files:**
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`

- [ ] **Step 1: Add the bar to XAML**

Open `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`. The grid currently has 4 rows (header, filter row, datagrid, export buttons). Add a new 5th row for the sticky bar. Change `<Grid.RowDefinitions>` to:

```xml
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
```

Then immediately **before** the final `<StackPanel Orientation="Horizontal" Grid.Row="3" ...>` (the export buttons), insert:

```xml
    <Border x:Name="ConfirmBar" Grid.Row="3" Background="#FFFFEFB0" BorderBrush="DarkGoldenrod"
            BorderThickness="0,1,0,1" Padding="8" Visibility="Collapsed">
      <StackPanel Orientation="Horizontal">
        <TextBlock x:Name="ConfirmText" VerticalAlignment="Center" FontWeight="Bold"/>
        <Button x:Name="ConfirmButton" Content="Confirm" Padding="8,4" Margin="16,0,8,0"/>
        <Button x:Name="ClearCartButton" Content="Clear" Padding="8,4"/>
        <TextBlock Text="(plugin doesn't dust for you — disenchant in-game manually)"
                   VerticalAlignment="Center" Margin="16,0,0,0" Foreground="DarkSlateGray" FontStyle="Italic"/>
      </StackPanel>
    </Border>
```

The existing export-buttons StackPanel was at Grid.Row="3". **Update it to Grid.Row="4"**:

```xml
    <StackPanel Orientation="Horizontal" Grid.Row="4" Margin="8" HorizontalAlignment="Right">
      <Button x:Name="ExportCsvButton" Content="Export CSV" Padding="8,4" Margin="0,0,8,0"/>
      <Button x:Name="ExportJsonButton" Content="Export JSON" Padding="8,4"/>
    </StackPanel>
```

- [ ] **Step 2: Wire selection state and the confirm action**

Open `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`. After the constructor's existing event wiring, append:

```csharp
            // Subscribe to IsSelected changes on each row to update the sticky bar.
            CompositionTarget.Rendering += (s, e) => UpdateConfirmBar();
            ConfirmButton.Click += (s, e) => ConfirmCart();
            ClearCartButton.Click += (s, e) => ClearCart();
```

Add `using System.Windows.Media;` at the top.

Inside the class, add these methods:

```csharp
        private void UpdateConfirmBar()
        {
            if (ItemsGrid.ItemsSource is System.Collections.Generic.IEnumerable<DustItemRow> rows)
            {
                int selected = 0;
                int dust = 0;
                foreach (var r in rows)
                {
                    if (r.IsSelected) { selected++; dust += r.DustGained; }
                }
                if (selected > 0)
                {
                    ConfirmText.Text = $"{selected} cards selected = {dust:n0} dust";
                    ConfirmBar.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    ConfirmBar.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }

        private void ConfirmCart()
        {
            // For v1.1 this is a session marker; we don't modify the game.
            // Just clear the selection — the user keeps the visual confirmation via a toast (Task 14).
            ClearCart();
        }

        private void ClearCart()
        {
            if (ItemsGrid.ItemsSource is System.Collections.Generic.IEnumerable<DustItemRow> rows)
            {
                foreach (var r in rows) r.IsSelected = false;
            }
        }
```

Note: using `CompositionTarget.Rendering` to detect IsSelected changes is a simple but inefficient choice. It fires on every WPF render frame. For a personal tool with <10k rows this is fine; if performance becomes an issue later, refactor to a proper observable collection. Document this limitation as a known trade-off.

- [ ] **Step 3: Build and test interactively**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: 0 errors. The confirm bar should appear when any checkbox is ticked, showing count and dust sum. Clear button uncheck-all works. Confirm button currently just clears (toast comes in Task 14).

- [ ] **Step 4: Commit**

```bash
git add src/DustAdvisor.Ui/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): sticky confirm bar shows selection count + dust sum"
```

---

## Task 12: SessionLedger + UndoStack (TDD pure classes)

**Files:**
- Create: `src/DustAdvisor.Ui.Export/SessionLedger.cs`
- Create: `src/DustAdvisor.Ui.Export/UndoStack.cs`
- Create: `src/DustAdvisor.Ui.Tests/UndoStackTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/DustAdvisor.Ui.Tests/UndoStackTests.cs`:

```csharp
using DustAdvisor.Ui.Export;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Ui.Tests
{
    public class UndoStackTests
    {
        [Fact]
        public void Push_then_Undo_invokes_inverse_action()
        {
            var stack = new UndoStack(maxDepth: 10);
            int counter = 0;
            stack.Push("inc", () => counter++);
            stack.Undo();
            counter.Should().Be(1);
        }

        [Fact]
        public void Undo_then_Redo_invokes_original_action()
        {
            var stack = new UndoStack(maxDepth: 10);
            int redoCount = 0;
            int undoCount = 0;
            stack.Push("test", undo: () => undoCount++, redo: () => redoCount++);
            stack.Undo();
            stack.Redo();
            undoCount.Should().Be(1);
            redoCount.Should().Be(1);
        }

        [Fact]
        public void MaxDepth_drops_oldest_when_exceeded()
        {
            var stack = new UndoStack(maxDepth: 2);
            int a = 0, b = 0, c = 0;
            stack.Push("a", () => a++);
            stack.Push("b", () => b++);
            stack.Push("c", () => c++);
            stack.Undo(); // should undo "c"
            stack.Undo(); // should undo "b"
            stack.Undo(); // "a" was dropped; should be a no-op
            a.Should().Be(0);
            b.Should().Be(1);
            c.Should().Be(1);
        }

        [Fact]
        public void Undo_returns_label_of_action_undone()
        {
            var stack = new UndoStack(maxDepth: 10);
            stack.Push("disenchant Yogg", () => { });
            stack.Undo().Should().Be("disenchant Yogg");
        }

        [Fact]
        public void Undo_returns_null_when_stack_empty()
        {
            var stack = new UndoStack(maxDepth: 10);
            stack.Undo().Should().BeNull();
        }
    }
}
```

- [ ] **Step 2: Run — fails**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Ui.Tests/DustAdvisor.Ui.Tests.csproj)" 2>&1 | tail -10
```

- [ ] **Step 3: Implement UndoStack and SessionLedger**

Create `src/DustAdvisor.Ui.Export/UndoStack.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace DustAdvisor.Ui.Export
{
    public sealed class UndoStack
    {
        private sealed class Entry
        {
            public string Label;
            public Action Undo;
            public Action Redo;
        }

        private readonly LinkedList<Entry> _undo = new LinkedList<Entry>();
        private readonly LinkedList<Entry> _redo = new LinkedList<Entry>();
        private readonly int _maxDepth;

        public UndoStack(int maxDepth)
        {
            _maxDepth = maxDepth;
        }

        public void Push(string label, Action undo, Action redo = null)
        {
            _undo.AddLast(new Entry { Label = label, Undo = undo, Redo = redo });
            while (_undo.Count > _maxDepth) _undo.RemoveFirst();
            _redo.Clear();
        }

        public string Undo()
        {
            if (_undo.Count == 0) return null;
            var entry = _undo.Last.Value;
            _undo.RemoveLast();
            entry.Undo?.Invoke();
            _redo.AddLast(entry);
            return entry.Label;
        }

        public string Redo()
        {
            if (_redo.Count == 0) return null;
            var entry = _redo.Last.Value;
            _redo.RemoveLast();
            entry.Redo?.Invoke();
            _undo.AddLast(entry);
            return entry.Label;
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
    }
}
```

Create `src/DustAdvisor.Ui.Export/SessionLedger.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace DustAdvisor.Ui.Export
{
    public sealed class SessionLedgerEntry
    {
        public DateTimeOffset At { get; }
        public int CardCount { get; }
        public int Dust { get; }
        public IReadOnlyList<string> CardIds { get; }

        public SessionLedgerEntry(DateTimeOffset at, int cardCount, int dust, IReadOnlyList<string> cardIds)
        {
            At = at;
            CardCount = cardCount;
            Dust = dust;
            CardIds = cardIds;
        }
    }

    public sealed class SessionLedger
    {
        private readonly List<SessionLedgerEntry> _entries = new List<SessionLedgerEntry>();
        public IReadOnlyList<SessionLedgerEntry> Entries => _entries;
        public int TotalDust { get; private set; }

        public void RecordConfirm(int dust, IReadOnlyList<string> cardIds)
        {
            _entries.Add(new SessionLedgerEntry(DateTimeOffset.UtcNow, cardIds.Count, dust, cardIds));
            TotalDust += dust;
        }

        public void Undo()
        {
            if (_entries.Count == 0) return;
            var last = _entries[_entries.Count - 1];
            _entries.RemoveAt(_entries.Count - 1);
            TotalDust -= last.Dust;
        }
    }
}
```

- [ ] **Step 4: Run — passes**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Ui.Tests/DustAdvisor.Ui.Tests.csproj)" 2>&1 | tail -5
```

Expected: 3 + 5 new = 8 Ui tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Ui.Export/ src/DustAdvisor.Ui.Tests/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): UndoStack and SessionLedger (pure C#, unit-tested)"
```

---

## Task 13: Toast notification component

**Files:**
- Create: `src/DustAdvisor.Ui/ToastHost.cs`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`

- [ ] **Step 1: Create ToastHost**

Create `src/DustAdvisor.Ui/ToastHost.cs`:

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DustAdvisor.Ui
{
    public sealed class ToastHost
    {
        private readonly StackPanel _container;

        public ToastHost(StackPanel container)
        {
            _container = container;
        }

        public void Show(string message, TimeSpan? linger = null, Action onClick = null)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x33, 0x33, 0x33)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 4, 0, 0),
                Cursor = onClick != null ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
            };
            var text = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 12,
            };
            border.Child = text;
            if (onClick != null)
                border.MouseLeftButtonUp += (s, e) => { onClick(); _container.Children.Remove(border); };

            _container.Children.Add(border);

            var timer = new DispatcherTimer { Interval = linger ?? TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (_container.Children.Contains(border))
                    _container.Children.Remove(border);
            };
            timer.Start();
        }
    }
}
```

- [ ] **Step 2: Add the toast container to XAML**

Open `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`. Wrap the existing `<Grid>` in a parent layout so the toast container floats over everything. Replace the root `<Grid>...</Grid>` opening structure with:

```xml
  <Grid>
    <Grid x:Name="MainGrid">
```

And close at the end with an extra `</Grid>` plus add the toast host before:

```xml
    </Grid>
    <StackPanel x:Name="ToastContainer" HorizontalAlignment="Right" VerticalAlignment="Bottom"
                Margin="0,0,16,16" IsHitTestVisible="True"/>
  </Grid>
```

So the final structure is `Window > Grid > [Grid x:Name="MainGrid" with all content] + [StackPanel x:Name="ToastContainer"]`.

All existing children move inside `MainGrid` (just rename the original `<Grid>` to `<Grid x:Name="MainGrid">`).

- [ ] **Step 3: Wire ToastHost in code-behind**

In `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`, add a field:

```csharp
        private ToastHost _toasts;
```

In the constructor, after `InitializeComponent()`:

```csharp
            _toasts = new ToastHost(ToastContainer);
```

Update `ConfirmCart` to record + toast:

```csharp
        private readonly DustAdvisor.Ui.Export.SessionLedger _ledger = new DustAdvisor.Ui.Export.SessionLedger();
        private readonly DustAdvisor.Ui.Export.UndoStack _undo = new DustAdvisor.Ui.Export.UndoStack(maxDepth: 100);

        private void ConfirmCart()
        {
            var rows = (ItemsGrid.ItemsSource as System.Collections.Generic.IEnumerable<DustItemRow>) ?? new DustItemRow[0];
            var selected = rows.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0) return;
            var dust = selected.Sum(r => r.DustGained);
            var cardIds = selected.Select(r => r.CardId).ToList();
            _ledger.RecordConfirm(dust, cardIds);
            _undo.Push(
                label: $"marked {cardIds.Count} cards",
                undo: () => { _ledger.Undo(); foreach (var r in selected) r.IsSelected = true; });
            foreach (var r in selected) r.IsSelected = false;
            _toasts.Show($"Marked {cardIds.Count} cards for disenchant ({dust:n0} dust)", onClick: () => { _undo.Undo(); });
        }
```

Place the field declarations near the top of the class (after `_artCache`).

- [ ] **Step 4: Build**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: 0 errors. Clicking Confirm now shows a clickable toast that undoes the confirm.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Ui/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): toast notifications with click-to-undo on confirm"
```

---

## Task 14: Right-click context menu (never-suggest + open in browser)

**Files:**
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`

- [ ] **Step 1: Add ContextMenu to the DataGridRow style**

Open `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`. Inside the `<Style TargetType="DataGridRow">`, add a `ContextMenu` setter:

```xml
          <Setter Property="ContextMenu">
            <Setter.Value>
              <ContextMenu>
                <MenuItem Header="Open in HearthstoneJSON" Click="ContextOpenBrowser_Click"/>
                <Separator/>
                <MenuItem Header="Never suggest (Regular)" Click="ContextNeverRegular_Click"/>
                <MenuItem Header="Never suggest (Golden)" Click="ContextNeverGolden_Click"/>
                <MenuItem Header="Never suggest (any premium)" Click="ContextNeverAny_Click"/>
              </ContextMenu>
            </Setter.Value>
          </Setter>
```

Place this inside the existing `<Style ...>` block, alongside the other `<Setter>`s.

- [ ] **Step 2: Implement handlers**

In `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`, add a field for the repo (the path is passed in via constructor) plus handlers. First, modify the constructor to accept the path:

```csharp
        private readonly DustAdvisor.Data.NeverSuggestRepository _neverRepo = new DustAdvisor.Data.NeverSuggestRepository();
        private readonly string _neverSuggestPath;

        public DustAdvisorWindow(DustPlan plan, Func<AdvisorOptions, DustPlan> recompute, CardArtCache artCache, string neverSuggestPath)
        {
            // ... existing body
            _neverSuggestPath = neverSuggestPath;
            // ... rest of constructor unchanged
```

Update the caller in `Plugin.cs` to pass `PluginPaths.NeverSuggestFile`:

```csharp
                var win = new DustAdvisor.Ui.DustAdvisorWindow(initialPlan, recompute, artCache, PluginPaths.NeverSuggestFile);
```

Add this helper and the three handlers at the bottom of the class:

```csharp
        private DustItemRow GetContextRow(object sender)
        {
            // sender is a MenuItem; navigate up to DataGridRow.
            if (sender is System.Windows.FrameworkElement mi && mi.DataContext is DustItemRow row)
                return row;
            return null;
        }

        private void ContextOpenBrowser_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var row = GetContextRow(sender);
            if (row == null) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"https://hearthstonejson.com/cards/{row.CardId}",
                    UseShellExecute = true,
                });
            }
            catch { /* swallow; this is best-effort */ }
        }

        private void AddNeverSuggest(string cardId, params DustAdvisor.Algorithm.Domain.Premium[] tiers)
        {
            var existing = new System.Collections.Generic.HashSet<(string, DustAdvisor.Algorithm.Domain.Premium)>(
                _neverRepo.Load(_neverSuggestPath));
            foreach (var t in tiers) existing.Add((cardId, t));
            _neverRepo.Save(_neverSuggestPath, existing);
            // Remove the row from the visible list (next recompute will exclude it too).
            if (ItemsGrid.ItemsSource is System.Collections.Generic.IEnumerable<DustItemRow> rows)
                ItemsGrid.ItemsSource = rows.Where(r => r.CardId != cardId).ToList();
            _toasts.Show($"Never suggest: {cardId}", onClick: () =>
            {
                var rollback = new System.Collections.Generic.HashSet<(string, DustAdvisor.Algorithm.Domain.Premium)>(_neverRepo.Load(_neverSuggestPath));
                foreach (var t in tiers) rollback.Remove((cardId, t));
                _neverRepo.Save(_neverSuggestPath, rollback);
                _toasts.Show($"Restored: {cardId}");
            });
        }

        private void ContextNeverRegular_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var row = GetContextRow(sender);
            if (row != null) AddNeverSuggest(row.CardId, DustAdvisor.Algorithm.Domain.Premium.Regular);
        }

        private void ContextNeverGolden_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var row = GetContextRow(sender);
            if (row != null) AddNeverSuggest(row.CardId, DustAdvisor.Algorithm.Domain.Premium.Golden);
        }

        private void ContextNeverAny_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var row = GetContextRow(sender);
            if (row != null) AddNeverSuggest(row.CardId,
                DustAdvisor.Algorithm.Domain.Premium.Regular,
                DustAdvisor.Algorithm.Domain.Premium.Golden,
                DustAdvisor.Algorithm.Domain.Premium.Signature);
        }
```

Note: the context menu DataContext on `MenuItem` is normally the row VM if the ContextMenu lives inside the DataGridRow style. In WPF, `ContextMenu`s don't inherit DataContext automatically through the visual tree — use `PlacementTarget.DataContext` if the simple `mi.DataContext` doesn't work. If the row turns out to be null at runtime, change `GetContextRow` to:

```csharp
        private DustItemRow GetContextRow(object sender)
        {
            if (sender is System.Windows.Controls.MenuItem mi
                && mi.Parent is System.Windows.Controls.ContextMenu cm
                && cm.PlacementTarget is System.Windows.FrameworkElement fe
                && fe.DataContext is DustItemRow row)
                return row;
            return null;
        }
```

- [ ] **Step 3: Build**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/DustAdvisor.Ui/ src/DustAdvisor.Hdt/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): right-click context menu with Never suggest + open in browser"
```

---

## Task 15: Keyboard shortcuts

**Files:**
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`

- [ ] **Step 1: Add Auto-advance checkbox to header**

In the XAML, find the second row's `StackPanel` (the filter row). After the `ShowGoldenBox`, append:

```xml
      <CheckBox x:Name="AutoAdvanceBox" Content="Auto-advance" VerticalAlignment="Center" Margin="16,0,0,0" IsChecked="True"/>
```

- [ ] **Step 2: Wire keyboard handler**

In `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`, in the constructor, add at the very end (after all other wiring):

```csharp
            this.PreviewKeyDown += DustAdvisorWindow_PreviewKeyDown;
```

Add the handler at the bottom of the class:

```csharp
        private void DustAdvisorWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Don't intercept keys when typing in the target-dust box.
            if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;

            bool ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
            bool shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;

            if (ctrl && e.Key == System.Windows.Input.Key.Z && !shift)
            {
                var label = _undo.Undo();
                if (label != null) _toasts.Show($"Undid: {label}");
                e.Handled = true;
                return;
            }
            if (ctrl && (e.Key == System.Windows.Input.Key.Y || (shift && e.Key == System.Windows.Input.Key.Z)))
            {
                var label = _undo.Redo();
                if (label != null) _toasts.Show($"Redid: {label}");
                e.Handled = true;
                return;
            }

            var row = ItemsGrid.SelectedItem as DustItemRow;
            if (row == null) return;

            switch (e.Key)
            {
                case System.Windows.Input.Key.D:
                    row.IsSelected = true;
                    if (AutoAdvanceBox.IsChecked == true) ItemsGrid.SelectedIndex = System.Math.Min(ItemsGrid.SelectedIndex + 1, ItemsGrid.Items.Count - 1);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.K:
                    AddNeverSuggest(row.CardId,
                        DustAdvisor.Algorithm.Domain.Premium.Regular,
                        DustAdvisor.Algorithm.Domain.Premium.Golden,
                        DustAdvisor.Algorithm.Domain.Premium.Signature);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Space:
                    row.IsSelected = !row.IsSelected;
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Enter:
                    var item = _plan.Items.FirstOrDefault(x => x.CardId == row.CardId);
                    if (item != null)
                    {
                        var win = new CardDetailWindow(item, _artCache) { Owner = this };
                        win.Show();
                    }
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Escape:
                    ClearCart();
                    e.Handled = true;
                    break;
            }
        }
```

- [ ] **Step 3: Build**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/DustAdvisor.Ui/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): keyboard shortcuts (D/K/Space/Enter/Esc/Ctrl+Z/Ctrl+Y)"
```

---

## Task 16: Status footer

**Files:**
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`

- [ ] **Step 1: Add a TextBlock above the export buttons**

In `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`, add another row to `<Grid.RowDefinitions>`:

```xml
      <RowDefinition Height="Auto"/>
```

Append a new row at index 5 (between the confirm bar at row 3 and the export buttons at row 4). Actually since the grid already has 5 rows (header/filter/grid/confirmBar/exports), add a 6th and place the status strip BEFORE the export buttons. The cleanest layout:

Replace the `<Grid.RowDefinitions>` with:

```xml
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>  <!-- 0: header -->
      <RowDefinition Height="Auto"/>  <!-- 1: filter row -->
      <RowDefinition Height="*"/>     <!-- 2: data grid -->
      <RowDefinition Height="Auto"/>  <!-- 3: confirm bar -->
      <RowDefinition Height="Auto"/>  <!-- 4: status footer -->
      <RowDefinition Height="Auto"/>  <!-- 5: export buttons -->
    </Grid.RowDefinitions>
```

Update the export-buttons StackPanel to `Grid.Row="5"`. Add a new status footer at Grid.Row="4":

```xml
    <TextBlock x:Name="StatusFooter" Grid.Row="4" Margin="8,4,8,0" Foreground="DimGray" FontSize="11"/>
```

- [ ] **Step 2: Update Render to populate StatusFooter**

In `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`, in the `Render` method, after `ItemsGrid.ItemsSource = ...`, add:

```csharp
            StatusFooter.Text = $"Session: {_ledger.Entries.Count} batches → {_ledger.TotalDust:n0} dust   |   Visible: {visible.Count} of {plan.Items.Count} plan rows";
```

Also call `Render(_plan)` after each `ConfirmCart`/`AddNeverSuggest`/`ClearCart` so the footer refreshes. Add a `Render(_plan);` line at the end of each of those methods.

- [ ] **Step 3: Build**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" 2>&1 | tail -8
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/DustAdvisor.Ui/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "feat(ui): status footer with session ledger and visible-row count"
```

---

## Task 17: Final release build, redeploy, manual smoke test

**Files:** none changed; this is integration.

- [ ] **Step 1: Build Release**

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" -c Release 2>&1 | tail -8
```

Expected: 0 errors. Tests: all pass.

- [ ] **Step 2: Refresh `dist/` and the live plugin folder**

```bash
ROOT=/mnt/g/Documentos/Projects/hearthstone
DIST="$ROOT/dist/DustAdvisor"
RELEASE="$ROOT/src/DustAdvisor.Hdt/bin/Release/net48"
PLUGIN_DIR=/mnt/c/Users/dharm/AppData/Roaming/HearthstoneDeckTracker/Plugins/DustAdvisor

cp "$RELEASE"/DustAdvisor*.dll "$DIST/"
cp "$DIST"/*.dll "$PLUGIN_DIR/"
ls -la "$PLUGIN_DIR/" | head -10
```

If the copy fails with "Input/output error", HDT is still running and holding the DLLs. Close HDT (system tray Exit), retry the copy, then restart HDT.

- [ ] **Step 3: Manual smoke test**

1. Close HDT fully, restart it. Confirm "Dust Advisor v0.1.0" still loads and is enabled.
2. Open Hearthstone, navigate to My Collection.
3. Click "Dust Advisor" in HDT menu. Window opens.
4. **Test hover preview**: hover over 3 random rows. Card art popup appears after ~300ms, follows cursor.
5. **Test double-click**: double-click a row. Detail window opens with 512x art and dust math. Press Escape — closes.
6. **Test selection cart**: tick 3 checkboxes. Sticky bar appears with count + dust. Click Confirm. Toast shows "Marked 3 cards…". Click the toast — selections are restored.
7. **Test target dust**: enter `1600` in Target dust input. List filters to the minimum subset. Status shows "X / 1600 target (N cards)".
8. **Test never-suggest**: right-click a row → "Never suggest (any premium)". Row disappears. Open `%APPDATA%\HearthstoneDeckTracker\Plugins\DustAdvisor\data\never_suggest.json` — verify the card is there. Click the toast — file is restored.
9. **Test keyboard**: click into the grid. Press D 3 times — 3 rows selected, focus advances. Ctrl+Z three times — all unselect. Enter on a row — detail window opens.
10. **Test status footer**: should always show session batches and visible row count.

- [ ] **Step 4: Move the tag**

```bash
cd /mnt/g/Documentos/Projects/hearthstone
git tag -d v1.0-personal
git tag -a v1.1-personal -m "Personal v1.1: interactivity (hover art, cart+undo, target dust, never-suggest, keyboard)"
git log --oneline -n 10
```

- [ ] **Step 5: Commit any final docs updates and report**

If you discovered any gaps in the spec or plan during the manual test, capture them as TODOs in `docs/specs/2026-05-14-interactivity-design.md` under a new "## Discovered During v1.1 Implementation" section, and commit:

```bash
git add docs/
git -c user.email=benjaserrau@gmail.com -c user.name="Benja Serrau" commit -m "docs: notes captured during v1.1 smoke test"
```

If nothing to note, skip.

---

## Done

What you built:
- Persistent "Never suggest" list with right-click + keyboard K, JSON-backed.
- Target-dust greedy optimizer with live UI filter.
- Card art hover preview + double-click detail window, with disk-cached HearthstoneJSON CDN.
- Selection cart with sticky confirm bar.
- Session ledger and undo stack with toast notifications (Ctrl+Z/Ctrl+Y).
- Keyboard shortcuts: D, K, Space, Enter, Esc, ↑/↓, Ctrl+Z/Y.
- Status footer showing session progress.

Tests: ~74 passing (43 algorithm + 23 data + 8 UI).

Maintenance notes carry over from v1.0:
- Update `StandardSets.Codes` on rotation.
- Add nerfed cards to `data/refund.json` with 14-day expiry.
- The `data/uncraftable.json` is still hand-curated; `never_suggest.json` is now also user-editable.
