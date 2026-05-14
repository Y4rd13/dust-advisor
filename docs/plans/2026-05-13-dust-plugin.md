# Hearthstone Dust Advisor Plugin — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local Windows HDT plugin (C#) that reads the user's Hearthstone collection and recommends which cards are safe to disenchant, with refund-window and rotation awareness.

**Architecture:** Four-project solution. `DustAdvisor.Algorithm` (pure netstandard2.0 library, no I/O) holds the core dust math. `DustAdvisor.Data` (netstandard2.0) fetches HearthstoneJSON metadata and loads curated `uncraftable.json` / `refund.json`. `DustAdvisor.Hdt` (net48) is the `IPlugin` entry point that bridges HDT's collection API to the algorithm. `DustAdvisor.Ui` (net48 WPF) is the visible panel. Tests run on net8.0 so they execute on the WSL dev box; production targets run on Windows.

**Tech Stack:** C# 7.3, .NET Standard 2.0 + .NET Framework 4.8 (WPF), Newtonsoft.Json (matches HDT), xUnit + FluentAssertions for tests, HDT plugin API.

**Reference spec:** `docs/specs/2026-05-13-dust-plugin-design.md` — Tasks below cite section numbers.

**Environment note:** The user's dev terminal is WSL Linux. Phases 0–2 (scaffolding, algorithm, data) build and test fully on Linux via `dotnet` CLI. Phases 3–5 (HDT plugin, WPF, integration) require Windows because HDT and WPF are Windows-only. Each task that requires Windows is marked `[Windows]`.

---

## Task 1: Bootstrap solution and .gitignore

**Files:**
- Create: `src/DustAdvisor.sln`
- Create: `.gitignore`
- Create: `README.md`

- [ ] **Step 1: Create the .gitignore**

Write to `/mnt/g/Documentos/Projects/hearthstone/.gitignore`:

```gitignore
# Build output
[Bb]in/
[Oo]bj/
[Dd]ebug/
[Rr]elease/
*.user
*.suo
.vs/

# NuGet
*.nupkg
packages/
.nuget/

# Cache directories
.cache/
artifacts/

# Editor
.vscode/
.idea/
*.swp
*~

# OS
.DS_Store
Thumbs.db

# Plugin runtime data
**/cache/cards.collectible.json
```

- [ ] **Step 2: Create the empty solution file**

Run from `/mnt/g/Documentos/Projects/hearthstone`:

```bash
mkdir -p src
cd src
dotnet new sln -n DustAdvisor
```

Expected output: `The template "Solution File" was created successfully.`

- [ ] **Step 3: Create README.md stub**

Write to `/mnt/g/Documentos/Projects/hearthstone/README.md`:

```markdown
# Hearthstone Dust Advisor

Personal HDT plugin that recommends which collected cards are safe to disenchant.

- Design spec: `docs/specs/2026-05-13-dust-plugin-design.md`
- Implementation plan: `docs/plans/2026-05-13-dust-plugin.md`

Local use only.
```

- [ ] **Step 4: Commit**

```bash
git add .gitignore README.md src/DustAdvisor.sln
git commit -m "chore: bootstrap solution and gitignore"
```

---

## Task 2: Create algorithm project (netstandard2.0) and its xUnit test project

**Files:**
- Create: `src/DustAdvisor.Algorithm/DustAdvisor.Algorithm.csproj`
- Create: `src/DustAdvisor.Algorithm.Tests/DustAdvisor.Algorithm.Tests.csproj`
- Modify: `src/DustAdvisor.sln`

- [ ] **Step 1: Create the algorithm class library**

Run from `/mnt/g/Documentos/Projects/hearthstone/src`:

```bash
dotnet new classlib -n DustAdvisor.Algorithm -f netstandard2.0
dotnet sln DustAdvisor.sln add DustAdvisor.Algorithm/DustAdvisor.Algorithm.csproj
rm DustAdvisor.Algorithm/Class1.cs
```

- [ ] **Step 2: Create the xUnit test project**

Run from `/mnt/g/Documentos/Projects/hearthstone/src`:

```bash
dotnet new xunit -n DustAdvisor.Algorithm.Tests -f net8.0
dotnet sln DustAdvisor.sln add DustAdvisor.Algorithm.Tests/DustAdvisor.Algorithm.Tests.csproj
dotnet add DustAdvisor.Algorithm.Tests/DustAdvisor.Algorithm.Tests.csproj reference DustAdvisor.Algorithm/DustAdvisor.Algorithm.csproj
dotnet add DustAdvisor.Algorithm.Tests/DustAdvisor.Algorithm.Tests.csproj package FluentAssertions
rm DustAdvisor.Algorithm.Tests/UnitTest1.cs
```

- [ ] **Step 3: Verify the solution builds**

Run from `/mnt/g/Documentos/Projects/hearthstone/src`:

```bash
dotnet build DustAdvisor.sln
```

Expected: `Build succeeded.` with 2 projects compiled.

- [ ] **Step 4: Verify tests run (no tests yet, but the runner should report 0/0)**

```bash
dotnet test DustAdvisor.sln
```

Expected: `Passed!  - Failed: 0, Passed: 0, Skipped: 0`

- [ ] **Step 5: Commit**

```bash
git add src/
git commit -m "feat(algorithm): scaffold netstandard2.0 library + xUnit tests"
```

---

## Task 3: Define core domain types

**Files:**
- Create: `src/DustAdvisor.Algorithm/Domain/Rarity.cs`
- Create: `src/DustAdvisor.Algorithm/Domain/Premium.cs`
- Create: `src/DustAdvisor.Algorithm/Domain/CardSet.cs`
- Create: `src/DustAdvisor.Algorithm/Domain/CardMeta.cs`
- Create: `src/DustAdvisor.Algorithm/Domain/CollectionEntry.cs`
- Create: `src/DustAdvisor.Algorithm/Domain/Strategy.cs`
- Create: `src/DustAdvisor.Algorithm/Domain/AdvisorOptions.cs`
- Create: `src/DustAdvisor.Algorithm/Domain/DustItem.cs`
- Create: `src/DustAdvisor.Algorithm/Domain/DustPlan.cs`
- Create: `src/DustAdvisor.Algorithm/Domain/Warning.cs`

These are scaffolding — no test cycle. Each file holds one type. Spec §6 fixes the shapes.

- [ ] **Step 1: Write `Rarity.cs`**

```csharp
namespace DustAdvisor.Algorithm.Domain
{
    public enum Rarity
    {
        Free,
        Common,
        Rare,
        Epic,
        Legendary,
    }
}
```

- [ ] **Step 2: Write `Premium.cs`**

```csharp
namespace DustAdvisor.Algorithm.Domain
{
    public enum Premium
    {
        Regular,
        Golden,
        Signature,
        Diamond,
    }
}
```

- [ ] **Step 3: Write `CardSet.cs`** (we keep set as a string for flexibility — HearthstoneJSON sets evolve every patch)

```csharp
namespace DustAdvisor.Algorithm.Domain
{
    public sealed class CardSet
    {
        public string Code { get; }
        public bool IsCore => Code == "CORE";
        public bool IsStandardLegal { get; }

        public CardSet(string code, bool isStandardLegal)
        {
            Code = code;
            IsStandardLegal = isStandardLegal;
        }
    }
}
```

- [ ] **Step 4: Write `CardMeta.cs`**

```csharp
namespace DustAdvisor.Algorithm.Domain
{
    public sealed class CardMeta
    {
        public string CardId { get; }
        public int DbfId { get; }
        public string Name { get; }
        public Rarity Rarity { get; }
        public CardSet Set { get; }
        public bool IsCollectible { get; }

        public CardMeta(string cardId, int dbfId, string name, Rarity rarity, CardSet set, bool isCollectible)
        {
            CardId = cardId;
            DbfId = dbfId;
            Name = name;
            Rarity = rarity;
            Set = set;
            IsCollectible = isCollectible;
        }
    }
}
```

- [ ] **Step 5: Write `CollectionEntry.cs`**

```csharp
namespace DustAdvisor.Algorithm.Domain
{
    public sealed class CollectionEntry
    {
        public string CardId { get; }
        public int Regular { get; }
        public int Golden { get; }
        public int Signature { get; }
        public int Diamond { get; }

        public CollectionEntry(string cardId, int regular = 0, int golden = 0, int signature = 0, int diamond = 0)
        {
            CardId = cardId;
            Regular = regular;
            Golden = golden;
            Signature = signature;
            Diamond = diamond;
        }
    }
}
```

- [ ] **Step 6: Write `Strategy.cs`**

```csharp
namespace DustAdvisor.Algorithm.Domain
{
    public enum Strategy
    {
        SafeOnly,
        MaxDust,
        RotationImminent,
        RefundOnly,
    }
}
```

- [ ] **Step 7: Write `AdvisorOptions.cs`**

```csharp
namespace DustAdvisor.Algorithm.Domain
{
    public sealed class AdvisorOptions
    {
        public Strategy Strategy { get; }
        public bool KeepStandardLegal { get; }
        public bool PreferGoldenForPlayset { get; }

        public AdvisorOptions(Strategy strategy = Strategy.SafeOnly,
                              bool keepStandardLegal = true,
                              bool preferGoldenForPlayset = true)
        {
            Strategy = strategy;
            KeepStandardLegal = keepStandardLegal;
            PreferGoldenForPlayset = preferGoldenForPlayset;
        }
    }
}
```

- [ ] **Step 8: Write `DustItem.cs`**

```csharp
namespace DustAdvisor.Algorithm.Domain
{
    public sealed class DustItem
    {
        public string CardId { get; }
        public string CardName { get; }
        public Rarity Rarity { get; }
        public int RegularToDust { get; }
        public int GoldenToDust { get; }
        public int DustGained { get; }
        public bool InRefundWindow { get; }
        public bool IsStandardLegal { get; }

        public DustItem(string cardId, string cardName, Rarity rarity,
                        int regularToDust, int goldenToDust, int dustGained,
                        bool inRefundWindow, bool isStandardLegal)
        {
            CardId = cardId;
            CardName = cardName;
            Rarity = rarity;
            RegularToDust = regularToDust;
            GoldenToDust = goldenToDust;
            DustGained = dustGained;
            InRefundWindow = inRefundWindow;
            IsStandardLegal = isStandardLegal;
        }
    }
}
```

- [ ] **Step 9: Write `Warning.cs`**

```csharp
namespace DustAdvisor.Algorithm.Domain
{
    public sealed class Warning
    {
        public string CardId { get; }
        public string Message { get; }

        public Warning(string cardId, string message)
        {
            CardId = cardId;
            Message = message;
        }
    }
}
```

- [ ] **Step 10: Write `DustPlan.cs`**

```csharp
using System.Collections.Generic;

namespace DustAdvisor.Algorithm.Domain
{
    public sealed class DustPlan
    {
        public IReadOnlyList<DustItem> Items { get; }
        public IReadOnlyList<Warning> Warnings { get; }
        public int TotalDust { get; }

        public DustPlan(IReadOnlyList<DustItem> items, IReadOnlyList<Warning> warnings, int totalDust)
        {
            Items = items;
            Warnings = warnings;
            TotalDust = totalDust;
        }
    }
}
```

- [ ] **Step 11: Verify it still builds**

```bash
cd /mnt/g/Documentos/Projects/hearthstone/src
dotnet build DustAdvisor.sln
```

Expected: `Build succeeded.`

- [ ] **Step 12: Commit**

```bash
git add src/DustAdvisor.Algorithm/Domain/
git commit -m "feat(algorithm): add core domain types (Rarity, Premium, CardMeta, CollectionEntry, AdvisorOptions, DustPlan)"
```

---

## Task 4: Add constants module (DE / craft / playset values)

**Files:**
- Create: `src/DustAdvisor.Algorithm/Constants.cs`
- Create: `src/DustAdvisor.Algorithm.Tests/ConstantsTests.cs`

Spec §6.1: DE values are 5/20/100/400 regular, 50/100/400/1600 golden; craft is 40/100/400/1600; playset is 1 for legendary else 2. Refund window dust equals craft cost.

- [ ] **Step 1: Write the failing test for DE values**

`src/DustAdvisor.Algorithm.Tests/ConstantsTests.cs`:

```csharp
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
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd /mnt/g/Documentos/Projects/hearthstone/src
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: build fails with `The name 'Constants' does not exist in the namespace`.

- [ ] **Step 3: Implement `Constants.cs`**

`src/DustAdvisor.Algorithm/Constants.cs`:

```csharp
using System;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm
{
    public static class Constants
    {
        public static int DisenchantRegular(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return 5;
                case Rarity.Rare: return 20;
                case Rarity.Epic: return 100;
                case Rarity.Legendary: return 400;
                default: throw new ArgumentOutOfRangeException(nameof(r), r, "Free/unknown rarity has no DE value");
            }
        }

        public static int DisenchantGolden(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return 50;
                case Rarity.Rare: return 100;
                case Rarity.Epic: return 400;
                case Rarity.Legendary: return 1600;
                default: throw new ArgumentOutOfRangeException(nameof(r), r, "Free/unknown rarity has no DE value");
            }
        }

        public static int CraftCost(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return 40;
                case Rarity.Rare: return 100;
                case Rarity.Epic: return 400;
                case Rarity.Legendary: return 1600;
                default: throw new ArgumentOutOfRangeException(nameof(r), r, "Free/unknown rarity has no craft cost");
            }
        }

        public static int PlaysetSize(Rarity r)
        {
            return r == Rarity.Legendary ? 1 : 2;
        }
    }
}
```

- [ ] **Step 4: Verify the tests pass**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: `Passed: 16` (4 theories × multiple inline data).

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Algorithm/Constants.cs src/DustAdvisor.Algorithm.Tests/ConstantsTests.cs
git commit -m "feat(algorithm): add DE/craft/playset constants with tests"
```

---

## Task 5: Algorithm — basic playset math (common, single tier)

The first behavioral test. Spec §6.3 pseudocode.

**Files:**
- Create: `src/DustAdvisor.Algorithm/Advisor.cs`
- Create: `src/DustAdvisor.Algorithm/AdvisorInputs.cs`
- Create: `src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`
- Create: `src/DustAdvisor.Algorithm.Tests/Fixtures/CardFixtures.cs`

- [ ] **Step 1: Write a fixture helper**

`src/DustAdvisor.Algorithm.Tests/Fixtures/CardFixtures.cs`:

```csharp
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm.Tests.Fixtures
{
    internal static class CardFixtures
    {
        private static readonly CardSet WildExpansion = new CardSet("OG", isStandardLegal: false);
        private static readonly CardSet StandardExpansion = new CardSet("BADLANDS", isStandardLegal: true);
        private static readonly CardSet Core = new CardSet("CORE", isStandardLegal: true);

        public static CardMeta CommonWild(string id, int dbfId, string name = "Common Card")
            => new CardMeta(id, dbfId, name, Rarity.Common, WildExpansion, isCollectible: true);

        public static CardMeta RareStandard(string id, int dbfId, string name = "Rare Card")
            => new CardMeta(id, dbfId, name, Rarity.Rare, StandardExpansion, isCollectible: true);

        public static CardMeta LegendaryWild(string id, int dbfId, string name = "Legendary Card")
            => new CardMeta(id, dbfId, name, Rarity.Legendary, WildExpansion, isCollectible: true);

        public static CardMeta CoreCard(string id, int dbfId, string name = "Core Card")
            => new CardMeta(id, dbfId, name, Rarity.Common, Core, isCollectible: true);
    }
}
```

- [ ] **Step 2: Write the failing first test — 5 regular commons, no goldens, expect 3 to dust at 5 each**

`src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`:

```csharp
using System.Collections.Generic;
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
    }
}
```

- [ ] **Step 3: Run the test to verify failure**

```bash
dotnet test DustAdvisor.Algorithm.Tests/ --filter AdvisorTests
```

Expected: build error (`Advisor`, `AdvisorInputs` undefined).

- [ ] **Step 4: Implement `AdvisorInputs`**

`src/DustAdvisor.Algorithm/AdvisorInputs.cs`:

```csharp
using System.Collections.Generic;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm
{
    public sealed class AdvisorInputs
    {
        public IReadOnlyList<CollectionEntry> Collection { get; }
        public IReadOnlyList<CardMeta> Meta { get; }
        public IReadOnlyCollection<(string CardId, Premium Premium)> Uncraftable { get; }
        public IReadOnlyCollection<string> RefundWindow { get; }
        public AdvisorOptions Options { get; }

        public AdvisorInputs(
            IReadOnlyList<CollectionEntry> collection,
            IReadOnlyList<CardMeta> meta,
            IReadOnlyCollection<(string, Premium)> uncraftable,
            IReadOnlyCollection<string> refundWindow,
            AdvisorOptions options)
        {
            Collection = collection;
            Meta = meta;
            Uncraftable = uncraftable;
            RefundWindow = refundWindow;
            Options = options;
        }
    }
}
```

- [ ] **Step 5: Implement the minimal Advisor**

`src/DustAdvisor.Algorithm/Advisor.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm
{
    public sealed class Advisor
    {
        public DustPlan Recommend(AdvisorInputs inputs)
        {
            var metaById = inputs.Meta.ToDictionary(m => m.CardId);
            var items = new List<DustItem>();
            var warnings = new List<Warning>();
            var totalDust = 0;

            foreach (var entry in inputs.Collection)
            {
                if (!metaById.TryGetValue(entry.CardId, out var meta)) continue;
                if (!meta.IsCollectible) continue;
                if (meta.Rarity == Rarity.Free) continue;

                int playset = Constants.PlaysetSize(meta.Rarity);
                int dustRegular = System.Math.Max(0, entry.Regular - playset);
                int dustGolden = 0;
                int dustGained = dustRegular * Constants.DisenchantRegular(meta.Rarity);

                if (dustRegular > 0 || dustGolden > 0)
                {
                    items.Add(new DustItem(
                        cardId: meta.CardId,
                        cardName: meta.Name,
                        rarity: meta.Rarity,
                        regularToDust: dustRegular,
                        goldenToDust: dustGolden,
                        dustGained: dustGained,
                        inRefundWindow: false,
                        isStandardLegal: meta.Set.IsStandardLegal));
                    totalDust += dustGained;
                }
            }

            return new DustPlan(items, warnings, totalDust);
        }
    }
}
```

- [ ] **Step 6: Verify the test passes**

```bash
dotnet test DustAdvisor.Algorithm.Tests/ --filter AdvisorTests
```

Expected: `Passed: 1`.

- [ ] **Step 7: Commit**

```bash
git add src/DustAdvisor.Algorithm/Advisor.cs src/DustAdvisor.Algorithm/AdvisorInputs.cs src/DustAdvisor.Algorithm.Tests/
git commit -m "feat(algorithm): minimal Advisor handles regular-copy excess"
```

---

## Task 6: Algorithm — legendaries use playset of 1

**Files:**
- Modify: `src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`

- [ ] **Step 1: Add the failing test**

Append inside the `AdvisorTests` class:

```csharp
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
```

- [ ] **Step 2: Run it — must pass already (algorithm uses `PlaysetSize`)**

```bash
dotnet test DustAdvisor.Algorithm.Tests/ --filter Legendaries_use_playset_of_one
```

Expected: `Passed: 1`. (If not, the bug is in `Constants.PlaysetSize` or `Advisor.Recommend`. Fix and rerun.)

- [ ] **Step 3: Commit**

```bash
git add src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs
git commit -m "test(algorithm): cover legendary playset of 1"
```

---

## Task 7: Algorithm — goldens count toward playset, regulars dusted first

Spec §6.3 — "Prefer to keep goldens as the playset (cosmetic upgrade), dust regulars first."

**Files:**
- Modify: `src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`
- Modify: `src/DustAdvisor.Algorithm/Advisor.cs`

- [ ] **Step 1: Add the failing test**

Append:

```csharp
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
```

- [ ] **Step 2: Run — they must fail**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: 2 failures (current algorithm ignores goldens).

- [ ] **Step 3: Update `Advisor.Recommend` to handle goldens**

Replace the body of the foreach loop in `src/DustAdvisor.Algorithm/Advisor.cs`:

```csharp
                int playset = Constants.PlaysetSize(meta.Rarity);

                // Prefer to keep goldens as the playset; dust regulars first.
                int keepGolden = System.Math.Min(entry.Golden, playset);
                int keepRegular = System.Math.Max(0, playset - keepGolden);
                int dustRegular = System.Math.Max(0, entry.Regular - keepRegular);
                int dustGolden = System.Math.Max(0, entry.Golden - keepGolden);

                int dustGained = dustRegular * Constants.DisenchantRegular(meta.Rarity)
                               + dustGolden * Constants.DisenchantGolden(meta.Rarity);

                if (dustRegular > 0 || dustGolden > 0)
                {
                    items.Add(new DustItem(
                        cardId: meta.CardId,
                        cardName: meta.Name,
                        rarity: meta.Rarity,
                        regularToDust: dustRegular,
                        goldenToDust: dustGolden,
                        dustGained: dustGained,
                        inRefundWindow: false,
                        isStandardLegal: meta.Set.IsStandardLegal));
                    totalDust += dustGained;
                }
```

- [ ] **Step 4: Run all tests, ensure green**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Algorithm/Advisor.cs src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs
git commit -m "feat(algorithm): goldens count toward playset; regulars dusted first"
```

---

## Task 8: Algorithm — skip Core, Free rarity, Diamond

Spec §6.3 — `if meta.set == "CORE": skip`, `if meta.rarity == FREE: skip`. Diamond copies are never dustable. (Signature uncraftable list will be handled in Task 9 via `uncraftable` set.)

**Files:**
- Modify: `src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`
- Modify: `src/DustAdvisor.Algorithm/Advisor.cs`

- [ ] **Step 1: Add tests**

Append:

```csharp
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
```

- [ ] **Step 2: Run — first two pass (Core already skipped — actually it isn't yet; verify), Diamond_copies fails (we currently allow nothing diamond into output but the playset logic doesn't account for diamonds)**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: `Core_set_cards_are_skipped` fails (Advisor doesn't yet skip Core). `Diamond_copies_are_not_dusted` passes trivially (no regulars/goldens means no item — coincidentally green). `Diamond_counts_toward_playset_so_regulars_become_dustable` may pass or fail depending on whether the playset slot is consumed by the diamond — currently it doesn't account for diamonds, so the regular IS dustable (because playset = 1, keepRegular = 1 - keepGolden(0) = 1, dustRegular = max(0, 1 - 1) = 0). So it fails on the assertion `RegularToDust.Should().Be(1)`.

Read each failure carefully before implementing.

- [ ] **Step 3: Update `Advisor.Recommend` to skip Core and to count diamond + signature toward playset**

Replace the foreach body with:

```csharp
                if (meta.Set.IsCore) continue;

                int playset = Constants.PlaysetSize(meta.Rarity);

                // Diamond and Signature count toward playset but cannot be dusted (Diamond never;
                // Signature only via the uncraftable set in a later task).
                int cosmeticHeld = entry.Diamond + entry.Signature;
                int playsetRemaining = System.Math.Max(0, playset - cosmeticHeld);

                int keepGolden = System.Math.Min(entry.Golden, playsetRemaining);
                int keepRegular = System.Math.Max(0, playsetRemaining - keepGolden);
                int dustRegular = System.Math.Max(0, entry.Regular - keepRegular);
                int dustGolden = System.Math.Max(0, entry.Golden - keepGolden);

                int dustGained = dustRegular * Constants.DisenchantRegular(meta.Rarity)
                               + dustGolden * Constants.DisenchantGolden(meta.Rarity);
```

(rest of the foreach body unchanged)

- [ ] **Step 4: Run all tests**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Algorithm/Advisor.cs src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs
git commit -m "feat(algorithm): skip Core/Free; Diamond and Signature count toward playset but never dust"
```

---

## Task 9: Algorithm — respect uncraftable set per (cardId, premium)

Spec §6.4 #3 — uncraftable list is keyed by `(CardId, Premium)`. A locked golden does not contribute to the dust gain even if it's "extra". Regulars of the same card may still be dustable.

**Files:**
- Modify: `src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`
- Modify: `src/DustAdvisor.Algorithm/Advisor.cs`

- [ ] **Step 1: Add tests**

```csharp
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
```

- [ ] **Step 2: Run — both fail**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: 2 failures.

- [ ] **Step 3: Update `Advisor.Recommend`**

Replace the dust-counting block (after the `cosmeticHeld` line) with:

```csharp
                bool regularLocked = inputs.Uncraftable.Contains((meta.CardId, Premium.Regular));
                bool goldenLocked = inputs.Uncraftable.Contains((meta.CardId, Premium.Golden));

                int effectiveRegular = regularLocked ? 0 : entry.Regular;
                int effectiveGolden = goldenLocked ? 0 : entry.Golden;
                // Locked copies do not count toward playset (you can't dust them, but the user may
                // not want to rely on them either — conservative: treat as cosmetic, not playset).
                // Diamond + Signature still count via cosmeticHeld above.

                int keepGolden = System.Math.Min(effectiveGolden, playsetRemaining);
                int keepRegular = System.Math.Max(0, playsetRemaining - keepGolden);
                int dustRegular = System.Math.Max(0, effectiveRegular - keepRegular);
                int dustGolden = System.Math.Max(0, effectiveGolden - keepGolden);
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: green.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Algorithm/Advisor.cs src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs
git commit -m "feat(algorithm): respect uncraftable set per (cardId, premium)"
```

---

## Task 10: Algorithm — refund window applies full craft cost

Spec §6.3 — `de_unit_reg = (cardId ∈ refundWindow) ? CRAFT[rarity] : DE_REG[rarity]`. Golden refund = 4× craft cost? Re-check the spec: in §6.3 the code shows `4 * CRAFT[rarity]` for golden refund. Verify against Hearthstone wiki: yes, golden refund returns full golden craft cost = 4× craft cost (except legendary which is 2× — wait, the spec table says golden legendary craft is 1600, same as regular; that's wrong. Re-read spec §6.1: golden Craft is 400/800/1600/3200 — regular is 40/100/400/1600. So golden legendary is 3200 craft. The "4×" rule applies for Common/Rare/Epic. Legendary golden craft is 2× regular = 3200.

Adjust the formula: `goldenCraftCost(rarity) = (rarity == Legendary ? 2 : 4) * CraftCost(rarity)`. Actually no — Spec §6.1 table says:

| Rarity | Craft (Regular) | Craft (Golden) |
|---|---|---|
| Common | 40 | 400 |
| Rare | 100 | 800 |
| Epic | 400 | 1600 |
| Legendary | 1600 | 3200 |

So golden craft = `10x` for common, `8x` for rare, `4x` for epic, `2x` for legendary. Not a simple multiplier. Better: add `Constants.GoldenCraftCost` explicitly.

**Files:**
- Modify: `src/DustAdvisor.Algorithm/Constants.cs`
- Modify: `src/DustAdvisor.Algorithm.Tests/ConstantsTests.cs`
- Modify: `src/DustAdvisor.Algorithm/Advisor.cs`
- Modify: `src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`

- [ ] **Step 1: Add a failing test for `Constants.GoldenCraftCost`**

In `ConstantsTests.cs`:

```csharp
        [Theory]
        [InlineData(Rarity.Common, 400)]
        [InlineData(Rarity.Rare, 800)]
        [InlineData(Rarity.Epic, 1600)]
        [InlineData(Rarity.Legendary, 3200)]
        public void GoldenCraftCost_returns_canonical_values(Rarity r, int expected)
        {
            Constants.GoldenCraftCost(r).Should().Be(expected);
        }
```

- [ ] **Step 2: Run — fails (no method)**

```bash
dotnet test DustAdvisor.Algorithm.Tests/ --filter GoldenCraftCost
```

Expected: build fail.

- [ ] **Step 3: Add `Constants.GoldenCraftCost`**

In `Constants.cs`, add:

```csharp
        public static int GoldenCraftCost(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return 400;
                case Rarity.Rare: return 800;
                case Rarity.Epic: return 1600;
                case Rarity.Legendary: return 3200;
                default: throw new ArgumentOutOfRangeException(nameof(r), r, "Free/unknown rarity has no golden craft cost");
            }
        }
```

- [ ] **Step 4: Run — passes**

```bash
dotnet test DustAdvisor.Algorithm.Tests/ --filter GoldenCraftCost
```

Expected: green.

- [ ] **Step 5: Add a failing Advisor test for the refund window**

In `AdvisorTests.cs`:

```csharp
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
```

- [ ] **Step 6: Run — both fail**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: 2 failures.

- [ ] **Step 7: Update `Advisor.Recommend` to apply refund pricing**

Replace the `dustGained` line and the `DustItem` construction with:

```csharp
                bool refund = inputs.RefundWindow.Contains(meta.CardId);
                int unitRegular = refund ? Constants.CraftCost(meta.Rarity) : Constants.DisenchantRegular(meta.Rarity);
                int unitGolden = refund ? Constants.GoldenCraftCost(meta.Rarity) : Constants.DisenchantGolden(meta.Rarity);

                int dustGained = dustRegular * unitRegular + dustGolden * unitGolden;

                if (dustRegular > 0 || dustGolden > 0)
                {
                    items.Add(new DustItem(
                        cardId: meta.CardId,
                        cardName: meta.Name,
                        rarity: meta.Rarity,
                        regularToDust: dustRegular,
                        goldenToDust: dustGolden,
                        dustGained: dustGained,
                        inRefundWindow: refund,
                        isStandardLegal: meta.Set.IsStandardLegal));
                    totalDust += dustGained;
                }
```

- [ ] **Step 8: Run all tests**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: green.

- [ ] **Step 9: Commit**

```bash
git add src/DustAdvisor.Algorithm/ src/DustAdvisor.Algorithm.Tests/
git commit -m "feat(algorithm): refund window pays full craft cost; add GoldenCraftCost constant"
```

---

## Task 11: Algorithm — strategy presets (SafeOnly, MaxDust, RotationImminent, RefundOnly)

Spec §6.5.

**Files:**
- Modify: `src/DustAdvisor.Algorithm/Advisor.cs`
- Modify: `src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`

- [ ] **Step 1: Add tests**

```csharp
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
```

- [ ] **Step 2: Run — all three fail**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

- [ ] **Step 3: Update `Advisor.Recommend`** — add Standard-legal handling and strategy filtering

Just before the `cosmeticHeld` line, add:

```csharp
                bool refund = inputs.RefundWindow.Contains(meta.CardId);
                if (inputs.Options.Strategy == Strategy.RefundOnly && !refund) continue;

                if (meta.Set.IsStandardLegal)
                {
                    warnings.Add(new Warning(meta.CardId,
                        "Standard-legal: card may still be valuable in current meta."));
                    if (inputs.Options.Strategy == Strategy.SafeOnly && inputs.Options.KeepStandardLegal) continue;
                }
```

And **remove** the redundant `bool refund = ...` line later in the method (since we now compute it earlier).

The final shape of the `Advisor.Recommend` foreach body should be:

```csharp
                if (!metaById.TryGetValue(entry.CardId, out var meta)) continue;
                if (!meta.IsCollectible) continue;
                if (meta.Rarity == Rarity.Free) continue;
                if (meta.Set.IsCore) continue;

                bool refund = inputs.RefundWindow.Contains(meta.CardId);
                if (inputs.Options.Strategy == Strategy.RefundOnly && !refund) continue;

                if (meta.Set.IsStandardLegal)
                {
                    warnings.Add(new Warning(meta.CardId,
                        "Standard-legal: card may still be valuable in current meta."));
                    if (inputs.Options.Strategy == Strategy.SafeOnly && inputs.Options.KeepStandardLegal) continue;
                }

                int playset = Constants.PlaysetSize(meta.Rarity);
                int cosmeticHeld = entry.Diamond + entry.Signature;
                int playsetRemaining = System.Math.Max(0, playset - cosmeticHeld);

                bool regularLocked = inputs.Uncraftable.Contains((meta.CardId, Premium.Regular));
                bool goldenLocked = inputs.Uncraftable.Contains((meta.CardId, Premium.Golden));
                int effectiveRegular = regularLocked ? 0 : entry.Regular;
                int effectiveGolden = goldenLocked ? 0 : entry.Golden;

                int keepGolden = System.Math.Min(effectiveGolden, playsetRemaining);
                int keepRegular = System.Math.Max(0, playsetRemaining - keepGolden);
                int dustRegular = System.Math.Max(0, effectiveRegular - keepRegular);
                int dustGolden = System.Math.Max(0, effectiveGolden - keepGolden);

                int unitRegular = refund ? Constants.CraftCost(meta.Rarity) : Constants.DisenchantRegular(meta.Rarity);
                int unitGolden = refund ? Constants.GoldenCraftCost(meta.Rarity) : Constants.DisenchantGolden(meta.Rarity);
                int dustGained = dustRegular * unitRegular + dustGolden * unitGolden;

                if (dustRegular > 0 || dustGolden > 0)
                {
                    items.Add(new DustItem(
                        cardId: meta.CardId,
                        cardName: meta.Name,
                        rarity: meta.Rarity,
                        regularToDust: dustRegular,
                        goldenToDust: dustGolden,
                        dustGained: dustGained,
                        inRefundWindow: refund,
                        isStandardLegal: meta.Set.IsStandardLegal));
                    totalDust += dustGained;
                }
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: green.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Algorithm/Advisor.cs src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs
git commit -m "feat(algorithm): strategy presets (SafeOnly/MaxDust/RefundOnly) and Standard-legal warning"
```

---

## Task 12: Algorithm — RotationImminent prioritization

For the personal MVP, "RotationImminent" doesn't bring different cards into the plan — it just sorts the plan so cards from the rotating-out year appear first. We carry this signal via `DustItem.IsStandardLegal == false && meta.Set.Code ∈ rotatingThisYear`. For v1 we'll defer the "rotatingThisYear" annotation to the metadata layer (a small static list); the algorithm only needs to sort by `(InRefundWindow desc, IsStandardLegal asc, Rarity desc)` so high-value & low-risk cards bubble up.

**Files:**
- Modify: `src/DustAdvisor.Algorithm/Advisor.cs`
- Modify: `src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`

- [ ] **Step 1: Add a sorting test**

```csharp
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
```

- [ ] **Step 2: Run — fails (or accidentally passes — verify)**

```bash
dotnet test DustAdvisor.Algorithm.Tests/ --filter Plan_is_sorted
```

If accidentally passes, change the input order to confirm sorting actually happens.

- [ ] **Step 3: Add an ordering step in `Advisor.Recommend`**

Replace the `return new DustPlan(items, warnings, totalDust);` line with:

```csharp
            var ordered = items
                .OrderByDescending(i => i.InRefundWindow)
                .ThenBy(i => i.IsStandardLegal)
                .ThenByDescending(i => i.Rarity)
                .ThenBy(i => i.CardName)
                .ToList();

            return new DustPlan(ordered, warnings, totalDust);
```

Make sure `using System.Linq;` is at the top of the file.

- [ ] **Step 4: Run all tests**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: green.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Algorithm/Advisor.cs src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs
git commit -m "feat(algorithm): sort plan refund-first, then Wild, then rarity desc"
```

---

## Task 13: Algorithm — realistic-collection smoke test

A single test that exercises everything: a few rarities, mix of regular/golden/diamond, one Core, one Free, one uncraftable, one refund.

**Files:**
- Modify: `src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs`

- [ ] **Step 1: Add the integration-style test**

```csharp
        [Fact]
        public void Realistic_mixed_collection_produces_correct_plan()
        {
            // Setup: 5 cards in collection.
            var freeCommon = new CardMeta("FREE_001", 9000, "Free Common", Rarity.Free,
                new CardSet("LEGACY", isStandardLegal: false), isCollectible: true);
            var coreCommon = CardFixtures.CoreCard("CORE_001", 9001);
            var wildRare = CardFixtures.RareStandard("STD_001", 9002); // wait — wildRare named, but it's Standard. Use the actual fixture call.
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
                    new CollectionEntry("WILD_LEG", regular: 1, golden: 1), // playset=1; keep 1 (golden preferred); dust 1 reg = 400
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
```

- [ ] **Step 2: Run — should pass on the existing algorithm**

```bash
dotnet test DustAdvisor.Algorithm.Tests/
```

Expected: green. If not, debug — the integration test exposes interaction bugs across rules.

- [ ] **Step 3: Commit**

```bash
git add src/DustAdvisor.Algorithm.Tests/AdvisorTests.cs
git commit -m "test(algorithm): realistic mixed-collection smoke test"
```

---

## Task 14: Data project — HearthstoneJSON client (interfaces + parsing)

Algorithm is done. Now build the data layer that produces `CardMeta` and the auxiliary lists from real sources. Same multi-target approach: netstandard2.0 production, net8.0 tests.

**Files:**
- Create: `src/DustAdvisor.Data/DustAdvisor.Data.csproj`
- Create: `src/DustAdvisor.Data/IHearthstoneJsonClient.cs`
- Create: `src/DustAdvisor.Data/HearthstoneJsonClient.cs`
- Create: `src/DustAdvisor.Data/HearthstoneJsonCardDto.cs`
- Create: `src/DustAdvisor.Data/StandardSets.cs`
- Create: `src/DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj`
- Create: `src/DustAdvisor.Data.Tests/HearthstoneJsonClientTests.cs`
- Create: `src/DustAdvisor.Data.Tests/Fixtures/sample_cards.json`

- [ ] **Step 1: Scaffold the projects**

```bash
cd /mnt/g/Documentos/Projects/hearthstone/src
dotnet new classlib -n DustAdvisor.Data -f netstandard2.0
rm DustAdvisor.Data/Class1.cs
dotnet sln DustAdvisor.sln add DustAdvisor.Data/DustAdvisor.Data.csproj
dotnet add DustAdvisor.Data/DustAdvisor.Data.csproj reference DustAdvisor.Algorithm/DustAdvisor.Algorithm.csproj
dotnet add DustAdvisor.Data/DustAdvisor.Data.csproj package Newtonsoft.Json

dotnet new xunit -n DustAdvisor.Data.Tests -f net8.0
rm DustAdvisor.Data.Tests/UnitTest1.cs
dotnet sln DustAdvisor.sln add DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj
dotnet add DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj reference DustAdvisor.Data/DustAdvisor.Data.csproj
dotnet add DustAdvisor.Data.Tests/DustAdvisor.Data.Tests.csproj package FluentAssertions
```

- [ ] **Step 2: Add the standard set list (2026 Year of the Scarab)**

Spec §6.3 — `isStandardLegal` is derived from a static list. Keep it tiny and update per year flip.

`src/DustAdvisor.Data/StandardSets.cs`:

```csharp
using System.Collections.Generic;

namespace DustAdvisor.Data
{
    /// Year of the Scarab (2026). Maintain by hand on rotation.
    public static class StandardSets
    {
        public static readonly IReadOnlySet<string> Codes = new HashSet<string>
        {
            "CORE",
            "ISLAND_VACATION",   // Travel to Un'Goro Plus / placeholder for current set codes
            "EMERALD_DREAM",
            "GREAT_DARK_BEYOND",
            "HEROES_OF_STARCRAFT", // depending on rotation date
            "BADLANDS",
            "WHIZBANGS_WORKSHOP",
        };
    }
}
```

Note: confirm the actual 2026 standard set codes from `https://www.hearthstonetopdecks.com/welcome-to-the-year-of-the-scarab-2026-hearthstone-standard-year-information/` and `https://hearthstone.wiki.gg/wiki/Standard` before the first real run. The codes above are placeholders; correct them in Task 21's smoke test before tagging.

- [ ] **Step 3: Define the DTO and interface**

`src/DustAdvisor.Data/HearthstoneJsonCardDto.cs`:

```csharp
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    internal sealed class HearthstoneJsonCardDto
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("dbfId")] public int DbfId { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("rarity")] public string Rarity { get; set; }
        [JsonProperty("set")] public string Set { get; set; }
        [JsonProperty("collectible")] public bool Collectible { get; set; }
    }
}
```

`src/DustAdvisor.Data/IHearthstoneJsonClient.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Data
{
    public interface IHearthstoneJsonClient
    {
        Task<IReadOnlyList<CardMeta>> LoadCollectibleAsync(string locale, CancellationToken ct);
    }
}
```

- [ ] **Step 4: Create a fixture file**

`src/DustAdvisor.Data.Tests/Fixtures/sample_cards.json`:

```json
[
  { "id": "EX1_001", "dbfId": 1, "name": "Common Wild", "rarity": "COMMON", "set": "OG", "collectible": true },
  { "id": "STD_001", "dbfId": 2, "name": "Standard Rare", "rarity": "RARE", "set": "BADLANDS", "collectible": true },
  { "id": "CORE_001", "dbfId": 3, "name": "Core Common", "rarity": "COMMON", "set": "CORE", "collectible": true },
  { "id": "NON_COLL", "dbfId": 4, "name": "Hero Power", "rarity": "FREE", "set": "HERO_SKINS", "collectible": false }
]
```

In `DustAdvisor.Data.Tests.csproj`, add an item group so the fixture is copied to the output directory:

```xml
<ItemGroup>
  <None Update="Fixtures\sample_cards.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 5: Write the failing test**

`src/DustAdvisor.Data.Tests/HearthstoneJsonClientTests.cs`:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class HearthstoneJsonClientTests
    {
        [Fact]
        public async Task LoadCollectibleAsync_parses_fixture_and_excludes_non_collectible()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_cards.json");
            var client = new HearthstoneJsonClient(new LocalFileFetcher(path));

            var cards = await client.LoadCollectibleAsync(locale: "enUS", ct: CancellationToken.None);

            cards.Should().HaveCount(3); // non-collectible "NON_COLL" excluded
            cards.Should().Contain(c => c.CardId == "EX1_001" && c.Set.IsStandardLegal == false);
            cards.Should().Contain(c => c.CardId == "STD_001" && c.Set.IsStandardLegal == true);
            cards.Should().Contain(c => c.CardId == "CORE_001" && c.Set.IsCore);
        }

        private sealed class LocalFileFetcher : IHttpFetcher
        {
            private readonly string _path;
            public LocalFileFetcher(string path) { _path = path; }
            public Task<string> GetAsync(string url, CancellationToken ct)
                => Task.FromResult(File.ReadAllText(_path));
        }
    }
}
```

- [ ] **Step 6: Run — fails (HearthstoneJsonClient and IHttpFetcher don't exist)**

```bash
dotnet test DustAdvisor.Data.Tests/
```

- [ ] **Step 7: Implement `IHttpFetcher` and `HearthstoneJsonClient`**

`src/DustAdvisor.Data/IHttpFetcher.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    public interface IHttpFetcher
    {
        Task<string> GetAsync(string url, CancellationToken ct);
    }
}
```

`src/DustAdvisor.Data/HearthstoneJsonClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Algorithm.Domain;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class HearthstoneJsonClient : IHearthstoneJsonClient
    {
        private const string BaseUrl = "https://api.hearthstonejson.com/v1/latest";
        private readonly IHttpFetcher _fetcher;

        public HearthstoneJsonClient(IHttpFetcher fetcher)
        {
            _fetcher = fetcher;
        }

        public async Task<IReadOnlyList<CardMeta>> LoadCollectibleAsync(string locale, CancellationToken ct)
        {
            var url = $"{BaseUrl}/{locale}/cards.collectible.json";
            var json = await _fetcher.GetAsync(url, ct).ConfigureAwait(false);
            var dtos = JsonConvert.DeserializeObject<List<HearthstoneJsonCardDto>>(json) ?? new List<HearthstoneJsonCardDto>();
            var result = new List<CardMeta>(dtos.Count);
            foreach (var d in dtos)
            {
                if (!d.Collectible) continue;
                var rarity = ParseRarity(d.Rarity);
                if (rarity == null) continue;
                var set = new CardSet(d.Set ?? "UNKNOWN", StandardSets.Codes.Contains(d.Set ?? string.Empty));
                result.Add(new CardMeta(d.Id, d.DbfId, d.Name ?? d.Id, rarity.Value, set, isCollectible: true));
            }
            return result;
        }

        private static Rarity? ParseRarity(string s)
        {
            switch (s)
            {
                case "FREE": return Rarity.Free;
                case "COMMON": return Rarity.Common;
                case "RARE": return Rarity.Rare;
                case "EPIC": return Rarity.Epic;
                case "LEGENDARY": return Rarity.Legendary;
                default: return null;
            }
        }
    }
}
```

- [ ] **Step 8: Run tests**

```bash
dotnet test DustAdvisor.Data.Tests/
```

Expected: green.

- [ ] **Step 9: Commit**

```bash
git add src/DustAdvisor.Data/ src/DustAdvisor.Data.Tests/
git commit -m "feat(data): HearthstoneJSON client with injected fetcher; parse collectible cards into CardMeta"
```

---

## Task 15: Data — HTTP fetcher implementation + on-disk cache

The default `IHttpFetcher` for production. Cache the body to `%AppData%/HearthstoneDeckTracker/DustAdvisor/cache/` so offline runs work.

**Files:**
- Create: `src/DustAdvisor.Data/CachingHttpFetcher.cs`
- Create: `src/DustAdvisor.Data.Tests/CachingHttpFetcherTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class CachingHttpFetcherTests
    {
        [Fact]
        public async Task GetAsync_writes_response_to_cache_and_reads_from_cache_when_inner_throws()
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "DustAdvisorCacheTest-" + Guid.NewGuid().ToString("N"));
            try
            {
                var stub = new StubInner();
                var sut = new CachingHttpFetcher(stub, cacheDir);

                stub.NextResponse = "FIRST";
                var first = await sut.GetAsync("https://x/cards.json", CancellationToken.None);
                first.Should().Be("FIRST");

                stub.ShouldThrow = true;
                var second = await sut.GetAsync("https://x/cards.json", CancellationToken.None);
                second.Should().Be("FIRST"); // returned from disk
            }
            finally
            {
                if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
            }
        }

        private sealed class StubInner : IHttpFetcher
        {
            public string NextResponse { get; set; }
            public bool ShouldThrow { get; set; }
            public Task<string> GetAsync(string url, CancellationToken ct)
            {
                if (ShouldThrow) throw new System.Net.Http.HttpRequestException("offline");
                return Task.FromResult(NextResponse);
            }
        }
    }
}
```

- [ ] **Step 2: Run — fails (no CachingHttpFetcher)**

```bash
dotnet test DustAdvisor.Data.Tests/
```

- [ ] **Step 3: Implement `CachingHttpFetcher`**

`src/DustAdvisor.Data/CachingHttpFetcher.cs`:

```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    public sealed class CachingHttpFetcher : IHttpFetcher
    {
        private readonly IHttpFetcher _inner;
        private readonly string _cacheDir;

        public CachingHttpFetcher(IHttpFetcher inner, string cacheDir)
        {
            _inner = inner;
            _cacheDir = cacheDir;
            Directory.CreateDirectory(_cacheDir);
        }

        public async Task<string> GetAsync(string url, CancellationToken ct)
        {
            var path = CachePath(url);
            try
            {
                var fresh = await _inner.GetAsync(url, ct).ConfigureAwait(false);
                File.WriteAllText(path, fresh, Encoding.UTF8);
                return fresh;
            }
            catch
            {
                if (File.Exists(path)) return File.ReadAllText(path, Encoding.UTF8);
                throw;
            }
        }

        private string CachePath(string url)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                var name = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant() + ".json";
                return Path.Combine(_cacheDir, name);
            }
        }
    }
}
```

- [ ] **Step 4: Add a production `HttpClientFetcher` for completeness**

`src/DustAdvisor.Data/HttpClientFetcher.cs`:

```csharp
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    public sealed class HttpClientFetcher : IHttpFetcher
    {
        private readonly HttpClient _http;
        public HttpClientFetcher(HttpClient http) { _http = http; }
        public async Task<string> GetAsync(string url, CancellationToken ct)
        {
            using (var resp = await _http.GetAsync(url, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }
}
```

- [ ] **Step 5: Tests pass**

```bash
dotnet test DustAdvisor.Data.Tests/
```

Expected: green.

- [ ] **Step 6: Commit**

```bash
git add src/DustAdvisor.Data/ src/DustAdvisor.Data.Tests/
git commit -m "feat(data): on-disk cache + HttpClient fetcher implementations"
```

---

## Task 16: Data — Uncraftable and Refund repositories

Spec §6.4 #3, #4. Both load from JSON files. The plugin ships with `data/uncraftable.json` and `data/refund.json` files inside the repo; the data layer reads them.

**Files:**
- Create: `src/DustAdvisor.Data/UncraftableRepository.cs`
- Create: `src/DustAdvisor.Data/RefundRepository.cs`
- Create: `src/DustAdvisor.Data.Tests/UncraftableRepositoryTests.cs`
- Create: `src/DustAdvisor.Data.Tests/RefundRepositoryTests.cs`
- Create: `src/DustAdvisor.Data.Tests/Fixtures/uncraftable.json`
- Create: `src/DustAdvisor.Data.Tests/Fixtures/refund.json`

- [ ] **Step 1: Add fixtures**

`src/DustAdvisor.Data.Tests/Fixtures/uncraftable.json`:

```json
[
  { "cardId": "REWARD_001", "premium": "Golden" },
  { "cardId": "TAVERN_001", "premium": "Signature" }
]
```

`src/DustAdvisor.Data.Tests/Fixtures/refund.json`:

```json
[
  { "cardId": "NERFED_001", "expiresUtc": "2099-01-01T00:00:00Z" },
  { "cardId": "EXPIRED_001", "expiresUtc": "2020-01-01T00:00:00Z" }
]
```

Add them to the `<None Update>` item group in `DustAdvisor.Data.Tests.csproj` so they're copied to output.

- [ ] **Step 2: Write the failing tests**

`src/DustAdvisor.Data.Tests/UncraftableRepositoryTests.cs`:

```csharp
using System.IO;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class UncraftableRepositoryTests
    {
        [Fact]
        public void Load_returns_keyed_tuples_for_all_entries()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "uncraftable.json");
            var repo = new UncraftableRepository();
            var set = repo.Load(path);

            set.Should().Contain(("REWARD_001", Premium.Golden));
            set.Should().Contain(("TAVERN_001", Premium.Signature));
            set.Should().HaveCount(2);
        }
    }
}
```

`src/DustAdvisor.Data.Tests/RefundRepositoryTests.cs`:

```csharp
using System;
using System.IO;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class RefundRepositoryTests
    {
        [Fact]
        public void Load_returns_only_unexpired_card_ids_relative_to_now()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "refund.json");
            var repo = new RefundRepository();
            var set = repo.Load(path, now: new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero));

            set.Should().Contain("NERFED_001");
            set.Should().NotContain("EXPIRED_001");
        }
    }
}
```

- [ ] **Step 3: Run — fail**

```bash
dotnet test DustAdvisor.Data.Tests/
```

- [ ] **Step 4: Implement repositories**

`src/DustAdvisor.Data/UncraftableRepository.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using DustAdvisor.Algorithm.Domain;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class UncraftableRepository
    {
        private sealed class Entry
        {
            [JsonProperty("cardId")] public string CardId { get; set; }
            [JsonProperty("premium")] public string Premium { get; set; }
        }

        public IReadOnlyCollection<(string CardId, Premium Premium)> Load(string path)
        {
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

`src/DustAdvisor.Data/RefundRepository.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class RefundRepository
    {
        private sealed class Entry
        {
            [JsonProperty("cardId")] public string CardId { get; set; }
            [JsonProperty("expiresUtc")] public DateTimeOffset ExpiresUtc { get; set; }
        }

        public IReadOnlyCollection<string> Load(string path, DateTimeOffset now)
        {
            var raw = File.ReadAllText(path);
            var entries = JsonConvert.DeserializeObject<List<Entry>>(raw) ?? new List<Entry>();
            var set = new HashSet<string>();
            foreach (var e in entries)
            {
                if (e.ExpiresUtc > now) set.Add(e.CardId);
            }
            return set;
        }
    }
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet test DustAdvisor.Data.Tests/
```

Expected: green.

- [ ] **Step 6: Add the production data files (stubs)**

`/mnt/g/Documentos/Projects/hearthstone/data/uncraftable.json`:

```json
[]
```

`/mnt/g/Documentos/Projects/hearthstone/data/refund.json`:

```json
[]
```

A README at `/mnt/g/Documentos/Projects/hearthstone/data/README.md`:

```markdown
# Curated data

## `uncraftable.json`

Array of `{ "cardId": "...", "premium": "Regular|Golden|Signature|Diamond" }`. Each entry
marks a specific copy as locked and excludes it from disenchant recommendations.

Update on each Hearthstone patch by checking the in-game "uncraftable" filter or the
patch notes for new Tavern Pass / Rewards Track / Achievement / Bundle cards.

## `refund.json`

Array of `{ "cardId": "...", "expiresUtc": "YYYY-MM-DDTHH:MM:SSZ" }`. Each entry marks a
card that is currently in its 14-day post-balance-change full-refund window. Entries are
automatically ignored when expired.
```

- [ ] **Step 7: Commit**

```bash
git add src/DustAdvisor.Data/ src/DustAdvisor.Data.Tests/ data/
git commit -m "feat(data): uncraftable + refund repositories; curated JSON stubs"
```

---

## Task 17: Data — DataSnapshot composer

A small façade that loads the three data sources and returns one object ready to feed `AdvisorInputs`.

**Files:**
- Create: `src/DustAdvisor.Data/DataSnapshot.cs`
- Create: `src/DustAdvisor.Data/DataLoader.cs`
- Create: `src/DustAdvisor.Data.Tests/DataLoaderTests.cs`

- [ ] **Step 1: Write the failing test**

`src/DustAdvisor.Data.Tests/DataLoaderTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class DataLoaderTests
    {
        [Fact]
        public async Task LoadAsync_composes_meta_uncraftable_and_refund()
        {
            var cardsPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_cards.json");
            var unPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "uncraftable.json");
            var refundPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "refund.json");

            var loader = new DataLoader(
                new HearthstoneJsonClient(new HearthstoneJsonClientTests.LocalFileFetcherPublic(cardsPath)),
                new UncraftableRepository(),
                new RefundRepository());

            var snapshot = await loader.LoadAsync(
                locale: "enUS",
                uncraftablePath: unPath,
                refundPath: refundPath,
                now: new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero),
                ct: CancellationToken.None);

            snapshot.Meta.Should().HaveCount(3);
            snapshot.Uncraftable.Should().HaveCount(2);
            snapshot.RefundWindow.Should().ContainSingle().Which.Should().Be("NERFED_001");
        }
    }
}
```

(Note: `LocalFileFetcherPublic` doesn't exist yet — Step 2 makes the existing private LocalFileFetcher in `HearthstoneJsonClientTests` public-internally so this test can reuse it.)

- [ ] **Step 2: Refactor `HearthstoneJsonClientTests.LocalFileFetcher` to be internal-public for reuse**

In `src/DustAdvisor.Data.Tests/HearthstoneJsonClientTests.cs`, rename the private class to `LocalFileFetcherPublic` and change its accessibility to `internal`:

```csharp
        internal sealed class LocalFileFetcherPublic : IHttpFetcher
        {
            private readonly string _path;
            public LocalFileFetcherPublic(string path) { _path = path; }
            public Task<string> GetAsync(string url, CancellationToken ct)
                => Task.FromResult(File.ReadAllText(_path));
        }
```

Update the call site in the existing test to use `LocalFileFetcherPublic`.

- [ ] **Step 3: Run — fails (DataLoader, DataSnapshot don't exist)**

```bash
dotnet test DustAdvisor.Data.Tests/
```

- [ ] **Step 4: Implement `DataSnapshot` and `DataLoader`**

`src/DustAdvisor.Data/DataSnapshot.cs`:

```csharp
using System.Collections.Generic;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Data
{
    public sealed class DataSnapshot
    {
        public IReadOnlyList<CardMeta> Meta { get; }
        public IReadOnlyCollection<(string CardId, Premium Premium)> Uncraftable { get; }
        public IReadOnlyCollection<string> RefundWindow { get; }

        public DataSnapshot(
            IReadOnlyList<CardMeta> meta,
            IReadOnlyCollection<(string, Premium)> uncraftable,
            IReadOnlyCollection<string> refundWindow)
        {
            Meta = meta;
            Uncraftable = uncraftable;
            RefundWindow = refundWindow;
        }
    }
}
```

`src/DustAdvisor.Data/DataLoader.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    public sealed class DataLoader
    {
        private readonly IHearthstoneJsonClient _hsj;
        private readonly UncraftableRepository _uncraftableRepo;
        private readonly RefundRepository _refundRepo;

        public DataLoader(IHearthstoneJsonClient hsj, UncraftableRepository uncraftableRepo, RefundRepository refundRepo)
        {
            _hsj = hsj;
            _uncraftableRepo = uncraftableRepo;
            _refundRepo = refundRepo;
        }

        public async Task<DataSnapshot> LoadAsync(
            string locale, string uncraftablePath, string refundPath, DateTimeOffset now, CancellationToken ct)
        {
            var meta = await _hsj.LoadCollectibleAsync(locale, ct).ConfigureAwait(false);
            var uncraftable = _uncraftableRepo.Load(uncraftablePath);
            var refund = _refundRepo.Load(refundPath, now);
            return new DataSnapshot(meta, uncraftable, refund);
        }
    }
}
```

- [ ] **Step 5: Run all tests**

```bash
dotnet test DustAdvisor.sln
```

Expected: all green across both test projects.

- [ ] **Step 6: Commit**

```bash
git add src/DustAdvisor.Data/ src/DustAdvisor.Data.Tests/
git commit -m "feat(data): DataLoader composes HearthstoneJSON + uncraftable + refund into DataSnapshot"
```

---

## Task 18: [Windows] HDT plugin project scaffolding

From here on, you need to switch to Windows where HDT and Hearthstone are installed (or a Windows machine with the .NET Framework 4.8 SDK and HDT installed for assembly references). The WSL Linux dev box can still pull the latest from git but cannot compile or run net48/WPF.

**Prerequisites on the Windows machine:**
- HDT installed (typically at `%LOCALAPPDATA%\HearthstoneDeckTracker\` or `C:\Program Files (x86)\Hearthstone Deck Tracker\`). Record this path; we'll call it `$HDT_PATH`.
- .NET Framework 4.8 Developer Pack: `https://dotnet.microsoft.com/download/dotnet-framework/net48`
- Visual Studio 2022 Community OR `dotnet` SDK 8.0+ (works for net48 builds with reference assemblies)

**Files:**
- Create: `src/DustAdvisor.Hdt/DustAdvisor.Hdt.csproj`
- Create: `src/DustAdvisor.Hdt/Plugin.cs`

- [ ] **Step 1: Scaffold the project**

On the Windows machine, from `src/`:

```bash
dotnet new classlib -n DustAdvisor.Hdt -f net48
rm DustAdvisor.Hdt/Class1.cs
dotnet sln DustAdvisor.sln add DustAdvisor.Hdt/DustAdvisor.Hdt.csproj
dotnet add DustAdvisor.Hdt/DustAdvisor.Hdt.csproj reference DustAdvisor.Algorithm/DustAdvisor.Algorithm.csproj
dotnet add DustAdvisor.Hdt/DustAdvisor.Hdt.csproj reference DustAdvisor.Data/DustAdvisor.Data.csproj
```

- [ ] **Step 2: Edit the csproj to reference HDT assemblies and target Windows**

Replace contents of `src/DustAdvisor.Hdt/DustAdvisor.Hdt.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <UseWPF>true</UseWPF>
    <AssemblyName>DustAdvisor</AssemblyName>
    <RootNamespace>DustAdvisor.Hdt</RootNamespace>
    <PlatformTarget>x86</PlatformTarget>
    <LangVersion>7.3</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="Hearthstone Deck Tracker">
      <HintPath>$(LOCALAPPDATA)\HearthstoneDeckTracker\Hearthstone Deck Tracker.exe</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="HearthDb">
      <HintPath>$(LOCALAPPDATA)\HearthstoneDeckTracker\HearthDb.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\DustAdvisor.Algorithm\DustAdvisor.Algorithm.csproj" />
    <ProjectReference Include="..\DustAdvisor.Data\DustAdvisor.Data.csproj" />
  </ItemGroup>
</Project>
```

If HDT is installed at a non-default path, set the `LOCALAPPDATA` env-var fallback or hardcode `<HintPath>`.

- [ ] **Step 3: Implement the minimal `IPlugin`**

`src/DustAdvisor.Hdt/Plugin.cs`:

```csharp
using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.Plugins;

namespace DustAdvisor.Hdt
{
    public sealed class Plugin : IPlugin
    {
        public string Name => "Dust Advisor";
        public string Description => "Recommends which collected cards are safe to disenchant.";
        public string ButtonText => "Open";
        public string Author => "personal";
        public Version Version => new Version(0, 1, 0);
        public MenuItem MenuItem { get; private set; }

        public void OnLoad()
        {
            MenuItem = new MenuItem { Header = "Dust Advisor" };
            MenuItem.Click += (s, e) => { /* wired in a later task */ };
        }

        public void OnUnload() { }
        public void OnButtonPress() { /* wired in a later task */ }
        public void OnUpdate() { }
    }
}
```

- [ ] **Step 4: Build**

```bash
dotnet build src/DustAdvisor.Hdt/DustAdvisor.Hdt.csproj
```

Expected: build succeeds. If the HDT reference path is wrong, fix the HintPath and retry.

- [ ] **Step 5: Smoke-test installation**

```bash
mkdir -p "$LOCALAPPDATA\HearthstoneDeckTracker\Plugins\DustAdvisor"
copy src\DustAdvisor.Hdt\bin\Debug\net48\DustAdvisor.dll "$LOCALAPPDATA\HearthstoneDeckTracker\Plugins\DustAdvisor\"
copy src\DustAdvisor.Hdt\bin\Debug\net48\DustAdvisor.Algorithm.dll "$LOCALAPPDATA\HearthstoneDeckTracker\Plugins\DustAdvisor\"
copy src\DustAdvisor.Hdt\bin\Debug\net48\DustAdvisor.Data.dll "$LOCALAPPDATA\HearthstoneDeckTracker\Plugins\DustAdvisor\"
copy src\DustAdvisor.Hdt\bin\Debug\net48\Newtonsoft.Json.dll "$LOCALAPPDATA\HearthstoneDeckTracker\Plugins\DustAdvisor\"
```

Start HDT, open Options → Tracker → Plugins. Expected: "Dust Advisor" is listed and can be enabled. The menu item "Dust Advisor" should appear in HDT's menu (does nothing yet).

- [ ] **Step 6: Commit**

```bash
git add src/DustAdvisor.Hdt/
git commit -m "feat(hdt): minimal IPlugin scaffold registering 'Dust Advisor' menu item"
```

---

## Task 19: [Windows] HDT bridge — read collection via HDT.RemoteCollection

HDT exposes the player's collection at `Hearthstone_Deck_Tracker.Hearthstone.RemoteCollection`. It returns each owned card with normal/premium counts. Map this to our `CollectionEntry[]`.

**Files:**
- Create: `src/DustAdvisor.Hdt/CollectionSnapshotReader.cs`

- [ ] **Step 1: Implement the reader**

`src/DustAdvisor.Hdt/CollectionSnapshotReader.cs`:

```csharp
using System.Collections.Generic;
using DustAdvisor.Algorithm.Domain;
using Hearthstone_Deck_Tracker.Hearthstone;

namespace DustAdvisor.Hdt
{
    internal static class CollectionSnapshotReader
    {
        public static IReadOnlyList<CollectionEntry> Read()
        {
            var snapshot = RemoteCollection.Instance.GetCollection();
            if (snapshot == null) return new List<CollectionEntry>();

            // RemoteCollection.GetCollection() returns CollectionCard[] with fields:
            //   string CardId, int Normal, int Golden, int Signature, int Diamond
            // Field names verified against HDT 1.52.6 source. If HDT API changes,
            // adjust the property names here.
            var entries = new List<CollectionEntry>(snapshot.Count);
            foreach (var card in snapshot)
            {
                entries.Add(new CollectionEntry(
                    cardId: card.CardId,
                    regular: card.Normal,
                    golden: card.Golden,
                    signature: card.Signature,
                    diamond: card.Diamond));
            }
            return entries;
        }
    }
}
```

> **Important:** Before relying on these field names, open HDT's bin folder, decompile or inspect `Hearthstone Deck Tracker.exe` with ILSpy / dotPeek to confirm the exact public properties of `RemoteCollection.GetCollection()` return type. The shape above is the one documented in HDT v1.52.6; later versions may rename `Signature` → `SignatureNormal` or split Diamond. **Adjust the property accesses to match what your installed HDT exposes** — this is the only place where we depend on HDT internals.

- [ ] **Step 2: Smoke-test by adding a one-shot log line in `OnLoad`**

Modify `Plugin.OnLoad` to call `CollectionSnapshotReader.Read()` and log how many entries it sees. This is throwaway — remove after verifying.

```csharp
        public void OnLoad()
        {
            MenuItem = new MenuItem { Header = "Dust Advisor" };
            MenuItem.Click += (s, e) =>
            {
                var entries = CollectionSnapshotReader.Read();
                System.Windows.MessageBox.Show($"Read {entries.Count} cards from collection.", "Dust Advisor");
            };
        }
```

Build, copy DLLs, run HDT with Hearthstone running and the collection screen open. Click the menu item. Expected: a popup showing a non-zero card count.

- [ ] **Step 3: Commit**

```bash
git add src/DustAdvisor.Hdt/
git commit -m "feat(hdt): CollectionSnapshotReader bridges HDT.RemoteCollection to CollectionEntry[]"
```

---

## Task 20: [Windows] Wire the algorithm end-to-end (text output, no UI yet)

Quick path: produce a TXT report in `%TEMP%` showing the plan. Validates the full pipeline before the UI exists.

**Files:**
- Modify: `src/DustAdvisor.Hdt/Plugin.cs`
- Create: `src/DustAdvisor.Hdt/PluginPaths.cs`

- [ ] **Step 1: Implement `PluginPaths`**

`src/DustAdvisor.Hdt/PluginPaths.cs`:

```csharp
using System;
using System.IO;
using System.Reflection;

namespace DustAdvisor.Hdt
{
    internal static class PluginPaths
    {
        public static string PluginRoot
        {
            get
            {
                var dll = Assembly.GetExecutingAssembly().Location;
                return Path.GetDirectoryName(dll) ?? string.Empty;
            }
        }

        public static string CacheDir => Path.Combine(PluginRoot, "cache");
        public static string UncraftableFile => Path.Combine(PluginRoot, "data", "uncraftable.json");
        public static string RefundFile => Path.Combine(PluginRoot, "data", "refund.json");
    }
}
```

- [ ] **Step 2: Wire the algorithm into the menu click**

Replace `Plugin.OnLoad` body with:

```csharp
        public void OnLoad()
        {
            MenuItem = new MenuItem { Header = "Dust Advisor" };
            MenuItem.Click += async (s, e) => await RunAsync();
        }

        private async System.Threading.Tasks.Task RunAsync()
        {
            try
            {
                var collection = CollectionSnapshotReader.Read();

                var http = new DustAdvisor.Data.CachingHttpFetcher(
                    new DustAdvisor.Data.HttpClientFetcher(new System.Net.Http.HttpClient()),
                    PluginPaths.CacheDir);
                var hsj = new DustAdvisor.Data.HearthstoneJsonClient(http);
                var loader = new DustAdvisor.Data.DataLoader(
                    hsj,
                    new DustAdvisor.Data.UncraftableRepository(),
                    new DustAdvisor.Data.RefundRepository());

                var data = await loader.LoadAsync(
                    locale: "enUS",
                    uncraftablePath: PluginPaths.UncraftableFile,
                    refundPath: PluginPaths.RefundFile,
                    now: System.DateTimeOffset.UtcNow,
                    ct: System.Threading.CancellationToken.None);

                var inputs = new DustAdvisor.Algorithm.AdvisorInputs(
                    collection, data.Meta, data.Uncraftable, data.RefundWindow,
                    new DustAdvisor.Algorithm.Domain.AdvisorOptions());

                var plan = new DustAdvisor.Algorithm.Advisor().Recommend(inputs);

                var report = $"Cards in plan: {plan.Items.Count}\nTotal dust: {plan.TotalDust}\nWarnings: {plan.Warnings.Count}";
                System.Windows.MessageBox.Show(report, "Dust Advisor");
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString(), "Dust Advisor: error");
            }
        }
```

- [ ] **Step 3: Bundle `data/*.json` into the plugin install**

Create a `data/` folder under the plugin install dir and copy the curated JSON stubs:

```bash
mkdir -p "$LOCALAPPDATA\HearthstoneDeckTracker\Plugins\DustAdvisor\data"
copy data\uncraftable.json "$LOCALAPPDATA\HearthstoneDeckTracker\Plugins\DustAdvisor\data\"
copy data\refund.json "$LOCALAPPDATA\HearthstoneDeckTracker\Plugins\DustAdvisor\data\"
```

- [ ] **Step 4: Build, redeploy, run**

Rebuild, copy DLLs to the plugin folder, start HDT + Hearthstone. Click "Dust Advisor" menu. Expected popup format: `Cards in plan: N, Total dust: M, Warnings: K` with non-zero numbers.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Hdt/
git commit -m "feat(hdt): wire algorithm end-to-end with MessageBox report (UI replaces this in next task)"
```

---

## Task 21: [Windows] WPF panel — read-only list of recommendations

**Files:**
- Create: `src/DustAdvisor.Ui/DustAdvisor.Ui.csproj`
- Create: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`
- Create: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`
- Create: `src/DustAdvisor.Ui/DustItemRow.cs`
- Modify: `src/DustAdvisor.Hdt/Plugin.cs`

- [ ] **Step 1: Scaffold the UI project**

```bash
cd src
dotnet new classlib -n DustAdvisor.Ui -f net48
rm DustAdvisor.Ui/Class1.cs
dotnet sln DustAdvisor.sln add DustAdvisor.Ui/DustAdvisor.Ui.csproj
dotnet add DustAdvisor.Ui/DustAdvisor.Ui.csproj reference DustAdvisor.Algorithm/DustAdvisor.Algorithm.csproj
```

Edit `src/DustAdvisor.Ui/DustAdvisor.Ui.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <UseWPF>true</UseWPF>
    <PlatformTarget>x86</PlatformTarget>
    <LangVersion>7.3</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\DustAdvisor.Algorithm\DustAdvisor.Algorithm.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the row VM**

`src/DustAdvisor.Ui/DustItemRow.cs`:

```csharp
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Ui
{
    public sealed class DustItemRow
    {
        public string CardId { get; }
        public string Name { get; }
        public string Rarity { get; }
        public int RegularToDust { get; }
        public int GoldenToDust { get; }
        public int DustGained { get; }
        public string Flag { get; }

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
    }
}
```

- [ ] **Step 3: Create the XAML**

`src/DustAdvisor.Ui/DustAdvisorWindow.xaml`:

```xml
<Window x:Class="DustAdvisor.Ui.DustAdvisorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Dust Advisor" Height="700" Width="700">
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <StackPanel Orientation="Horizontal" Grid.Row="0" Margin="8">
      <TextBlock x:Name="TotalDustLabel" FontSize="22" FontWeight="Bold" Margin="0,0,16,0"/>
      <TextBlock x:Name="WarningCountLabel" VerticalAlignment="Center" Foreground="DarkOrange"/>
    </StackPanel>

    <DataGrid x:Name="ItemsGrid" Grid.Row="1" Margin="8" AutoGenerateColumns="False" IsReadOnly="True">
      <DataGrid.Columns>
        <DataGridTextColumn Header="Card" Binding="{Binding Name}" Width="220"/>
        <DataGridTextColumn Header="Rarity" Binding="{Binding Rarity}" Width="90"/>
        <DataGridTextColumn Header="Reg" Binding="{Binding RegularToDust}" Width="50"/>
        <DataGridTextColumn Header="Gold" Binding="{Binding GoldenToDust}" Width="55"/>
        <DataGridTextColumn Header="Dust" Binding="{Binding DustGained}" Width="80"/>
        <DataGridTextColumn Header="Flag" Binding="{Binding Flag}" Width="100"/>
      </DataGrid.Columns>
    </DataGrid>

    <StackPanel Orientation="Horizontal" Grid.Row="2" Margin="8" HorizontalAlignment="Right">
      <Button x:Name="ExportCsvButton" Content="Export CSV" Padding="8,4" Margin="0,0,8,0"/>
      <Button x:Name="ExportJsonButton" Content="Export JSON" Padding="8,4"/>
    </StackPanel>
  </Grid>
</Window>
```

`src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Ui
{
    public partial class DustAdvisorWindow : Window
    {
        public DustAdvisorWindow(DustPlan plan)
        {
            InitializeComponent();
            Render(plan);
        }

        public void Render(DustPlan plan)
        {
            TotalDustLabel.Text = $"{plan.TotalDust:n0} dust";
            WarningCountLabel.Text = plan.Warnings.Count > 0 ? $"{plan.Warnings.Count} warning(s)" : string.Empty;
            ItemsGrid.ItemsSource = plan.Items.Select(i => new DustItemRow(i)).ToList();
        }
    }
}
```

- [ ] **Step 4: Wire the Plugin to open the window instead of a MessageBox**

In `src/DustAdvisor.Hdt/DustAdvisor.Hdt.csproj`, add:

```xml
  <ItemGroup>
    <ProjectReference Include="..\DustAdvisor.Ui\DustAdvisor.Ui.csproj" />
  </ItemGroup>
```

In `Plugin.RunAsync`, replace the `MessageBox.Show(report, ...)` line with:

```csharp
                var win = new DustAdvisor.Ui.DustAdvisorWindow(plan);
                win.Show();
```

- [ ] **Step 5: Build, deploy, run**

```bash
dotnet build src/DustAdvisor.Hdt/
copy src\DustAdvisor.Hdt\bin\Debug\net48\*.dll "$LOCALAPPDATA\HearthstoneDeckTracker\Plugins\DustAdvisor\"
```

Restart HDT, click "Dust Advisor". Expected: the WPF window opens with a populated grid and a non-zero total.

- [ ] **Step 6: Commit**

```bash
git add src/DustAdvisor.Hdt/ src/DustAdvisor.Ui/
git commit -m "feat(ui): WPF panel renders DustPlan; Plugin opens window on menu click"
```

---

## Task 22: [Windows] CSV / JSON export

**Files:**
- Create: `src/DustAdvisor.Ui/Exporters/CsvExporter.cs`
- Create: `src/DustAdvisor.Ui/Exporters/JsonExporter.cs`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`
- Create: `src/DustAdvisor.Ui.Tests/DustAdvisor.Ui.Tests.csproj` (small test project for exporters)
- Create: `src/DustAdvisor.Ui.Tests/CsvExporterTests.cs`

- [ ] **Step 1: Scaffold the test project**

```bash
cd src
dotnet new xunit -n DustAdvisor.Ui.Tests -f net8.0
rm DustAdvisor.Ui.Tests/UnitTest1.cs
dotnet sln DustAdvisor.sln add DustAdvisor.Ui.Tests/DustAdvisor.Ui.Tests.csproj
dotnet add DustAdvisor.Ui.Tests/DustAdvisor.Ui.Tests.csproj reference DustAdvisor.Algorithm/DustAdvisor.Algorithm.csproj
dotnet add DustAdvisor.Ui.Tests/DustAdvisor.Ui.Tests.csproj package FluentAssertions
```

Because `DustAdvisor.Ui` targets net48 and pulls WPF, the exporters cannot live in the same csproj if we want net8 tests. Put `CsvExporter` and `JsonExporter` in a tiny intermediate library `DustAdvisor.Ui.Export` targeting netstandard2.0, then reference it from both UI and tests.

```bash
dotnet new classlib -n DustAdvisor.Ui.Export -f netstandard2.0
rm DustAdvisor.Ui.Export/Class1.cs
dotnet sln DustAdvisor.sln add DustAdvisor.Ui.Export/DustAdvisor.Ui.Export.csproj
dotnet add DustAdvisor.Ui.Export/DustAdvisor.Ui.Export.csproj reference DustAdvisor.Algorithm/DustAdvisor.Algorithm.csproj
dotnet add DustAdvisor.Ui.Export/DustAdvisor.Ui.Export.csproj package Newtonsoft.Json
dotnet add DustAdvisor.Ui/DustAdvisor.Ui.csproj reference DustAdvisor.Ui.Export/DustAdvisor.Ui.Export.csproj
dotnet add DustAdvisor.Ui.Tests/DustAdvisor.Ui.Tests.csproj reference DustAdvisor.Ui.Export/DustAdvisor.Ui.Export.csproj
```

- [ ] **Step 2: Failing test**

`src/DustAdvisor.Ui.Tests/CsvExporterTests.cs`:

```csharp
using System.Collections.Generic;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Ui.Export;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Ui.Tests
{
    public class CsvExporterTests
    {
        [Fact]
        public void ToCsv_emits_header_and_one_row_per_item()
        {
            var plan = new DustPlan(
                items: new List<DustItem>
                {
                    new DustItem("A", "Alpha", Rarity.Common, regularToDust: 3, goldenToDust: 0,
                                 dustGained: 15, inRefundWindow: false, isStandardLegal: false)
                },
                warnings: new List<Warning>(),
                totalDust: 15);

            var csv = CsvExporter.ToCsv(plan);
            csv.Should().StartWith("CardId,Name,Rarity,RegularToDust,GoldenToDust,DustGained,InRefundWindow,IsStandardLegal");
            csv.Should().Contain("A,Alpha,Common,3,0,15,False,False");
        }
    }
}
```

- [ ] **Step 3: Run — fails**

- [ ] **Step 4: Implement exporters**

`src/DustAdvisor.Ui.Export/CsvExporter.cs`:

```csharp
using System.Globalization;
using System.Text;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Ui.Export
{
    public static class CsvExporter
    {
        public static string ToCsv(DustPlan plan)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CardId,Name,Rarity,RegularToDust,GoldenToDust,DustGained,InRefundWindow,IsStandardLegal");
            foreach (var i in plan.Items)
            {
                sb.AppendLine(string.Join(",",
                    Escape(i.CardId), Escape(i.CardName), i.Rarity,
                    i.RegularToDust.ToString(CultureInfo.InvariantCulture),
                    i.GoldenToDust.ToString(CultureInfo.InvariantCulture),
                    i.DustGained.ToString(CultureInfo.InvariantCulture),
                    i.InRefundWindow, i.IsStandardLegal));
            }
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            if (s == null) return string.Empty;
            if (s.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
```

`src/DustAdvisor.Ui.Export/JsonExporter.cs`:

```csharp
using DustAdvisor.Algorithm.Domain;
using Newtonsoft.Json;

namespace DustAdvisor.Ui.Export
{
    public static class JsonExporter
    {
        public static string ToJson(DustPlan plan)
            => JsonConvert.SerializeObject(plan, Formatting.Indented);
    }
}
```

- [ ] **Step 5: Wire buttons in `DustAdvisorWindow.xaml.cs`**

Add a constructor field for the current plan and wire `Click`:

```csharp
        private readonly DustPlan _plan;

        public DustAdvisorWindow(DustPlan plan)
        {
            InitializeComponent();
            _plan = plan;
            Render(plan);
            ExportCsvButton.Click += (s, e) => SaveAs("CSV (*.csv)|*.csv", DustAdvisor.Ui.Export.CsvExporter.ToCsv(_plan));
            ExportJsonButton.Click += (s, e) => SaveAs("JSON (*.json)|*.json", DustAdvisor.Ui.Export.JsonExporter.ToJson(_plan));
        }

        private void SaveAs(string filter, string content)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = filter };
            if (dlg.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(dlg.FileName, content, System.Text.Encoding.UTF8);
            }
        }
```

- [ ] **Step 6: Run tests, build, redeploy**

```bash
dotnet test DustAdvisor.sln
```

Expected: all green.

Rebuild plugin, copy DLLs into the install dir, restart HDT. Click "Dust Advisor" → click Export CSV → save to disk → open the CSV in a text editor and verify rows match the grid.

- [ ] **Step 7: Commit**

```bash
git add src/DustAdvisor.Ui/ src/DustAdvisor.Ui.Export/ src/DustAdvisor.Ui.Tests/
git commit -m "feat(ui): CSV and JSON export buttons"
```

---

## Task 23: [Windows] Strategy and KeepStandardLegal controls in the panel

Add a Strategy dropdown and a "Keep Standard-legal" checkbox. Re-run algorithm on change.

**Files:**
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml`
- Modify: `src/DustAdvisor.Ui/DustAdvisorWindow.xaml.cs`
- Modify: `src/DustAdvisor.Hdt/Plugin.cs`

- [ ] **Step 1: Add controls to XAML**

In `DustAdvisorWindow.xaml`, replace the first `StackPanel` (Row 0) with:

```xml
    <StackPanel Orientation="Horizontal" Grid.Row="0" Margin="8">
      <TextBlock x:Name="TotalDustLabel" FontSize="22" FontWeight="Bold" Margin="0,0,16,0"/>
      <TextBlock x:Name="WarningCountLabel" VerticalAlignment="Center" Foreground="DarkOrange" Margin="0,0,16,0"/>
      <TextBlock Text="Strategy:" VerticalAlignment="Center" Margin="0,0,4,0"/>
      <ComboBox x:Name="StrategyBox" VerticalAlignment="Center" Width="160" Margin="0,0,12,0"/>
      <CheckBox x:Name="KeepStandardBox" Content="Keep Standard-legal" VerticalAlignment="Center" IsChecked="True"/>
    </StackPanel>
```

- [ ] **Step 2: Wire controls**

Replace the constructor of `DustAdvisorWindow`:

```csharp
        private DustPlan _plan;
        private readonly System.Func<DustAdvisor.Algorithm.Domain.AdvisorOptions, DustPlan> _recompute;

        public DustAdvisorWindow(DustPlan plan, System.Func<DustAdvisor.Algorithm.Domain.AdvisorOptions, DustPlan> recompute)
        {
            InitializeComponent();
            _plan = plan;
            _recompute = recompute;
            StrategyBox.ItemsSource = System.Enum.GetValues(typeof(DustAdvisor.Algorithm.Domain.Strategy));
            StrategyBox.SelectedItem = DustAdvisor.Algorithm.Domain.Strategy.SafeOnly;
            Render(plan);
            StrategyBox.SelectionChanged += (s, e) => Recompute();
            KeepStandardBox.Checked += (s, e) => Recompute();
            KeepStandardBox.Unchecked += (s, e) => Recompute();
            ExportCsvButton.Click += (s, e) => SaveAs("CSV (*.csv)|*.csv", DustAdvisor.Ui.Export.CsvExporter.ToCsv(_plan));
            ExportJsonButton.Click += (s, e) => SaveAs("JSON (*.json)|*.json", DustAdvisor.Ui.Export.JsonExporter.ToJson(_plan));
        }

        private void Recompute()
        {
            var opts = new DustAdvisor.Algorithm.Domain.AdvisorOptions(
                strategy: (DustAdvisor.Algorithm.Domain.Strategy)StrategyBox.SelectedItem,
                keepStandardLegal: KeepStandardBox.IsChecked == true);
            _plan = _recompute(opts);
            Render(_plan);
        }
```

- [ ] **Step 3: Update the Plugin to pass the recompute closure**

In `Plugin.RunAsync`, change the window construction to:

```csharp
                System.Func<DustAdvisor.Algorithm.Domain.AdvisorOptions, DustAdvisor.Algorithm.Domain.DustPlan> recompute = opts =>
                {
                    var ins = new DustAdvisor.Algorithm.AdvisorInputs(collection, data.Meta, data.Uncraftable, data.RefundWindow, opts);
                    return new DustAdvisor.Algorithm.Advisor().Recommend(ins);
                };
                var initialPlan = recompute(new DustAdvisor.Algorithm.Domain.AdvisorOptions());
                var win = new DustAdvisor.Ui.DustAdvisorWindow(initialPlan, recompute);
                win.Show();
```

- [ ] **Step 4: Build, redeploy, test**

Restart HDT, open Dust Advisor, switch the Strategy dropdown between SafeOnly and MaxDust. Expected: the dust total and rows change. Toggle the checkbox — Standard-legal rows appear/disappear.

- [ ] **Step 5: Commit**

```bash
git add src/DustAdvisor.Ui/ src/DustAdvisor.Hdt/
git commit -m "feat(ui): Strategy and KeepStandardLegal controls with live recompute"
```

---

## Task 24: [Windows] Manual integration verification

Spec §12 — manually verify ≥20 sampled rows match the in-game disenchant values.

- [ ] **Step 1: Run the plugin against your live collection**

Start HDT and Hearthstone. Open the Collection screen in-game so HDT/HearthMirror sees the data. Click "Dust Advisor" in HDT's menu.

- [ ] **Step 2: Sanity-check the total**

Compare `Total dust` in the window to the value reported by HDT's own "My Collection" sync (HDT shows total disenchantable dust in its UI). They should be close; small differences indicate Standard-legal / refund-window adjustments — review warnings to confirm.

- [ ] **Step 3: Sample 20 rows**

Pick 20 random rows from the grid (mix of commons, rares, epics, legendaries, and any goldens). For each:

1. Find the card in the in-game Collection.
2. Click Disenchant.
3. Confirm the dust value Hearthstone offers matches the per-row `DustGained / (RegularToDust + GoldenToDust * goldenRatio)` — or simpler: the per-copy value matches the constants in `Constants.cs`.
4. Cancel out before actually disenchanting (unless you really want to dust).

Document any discrepancies in `docs/specs/2026-05-13-dust-plugin-design.md` under §12 with the card and the observed-vs-expected delta.

- [ ] **Step 4: Tag a personal v1**

If all 20 match:

```bash
git tag -a v1.0-personal -m "Personal v1: dust advisor verified against live collection"
```

- [ ] **Step 5: Document the install on the README**

Append to `/mnt/g/Documentos/Projects/hearthstone/README.md`:

```markdown
## Install (Windows, local)

1. Build: `cd src && dotnet build DustAdvisor.sln -c Release`
2. Copy `src/DustAdvisor.Hdt/bin/Release/net48/*.dll` to `%LOCALAPPDATA%\HearthstoneDeckTracker\Plugins\DustAdvisor\`
3. Copy `data/uncraftable.json` and `data/refund.json` to `%LOCALAPPDATA%\HearthstoneDeckTracker\Plugins\DustAdvisor\data\`
4. Restart HDT. Enable "Dust Advisor" in Options → Tracker → Plugins.
5. Open Hearthstone, navigate to Collection, then click HDT's "Dust Advisor" menu item.
```

- [ ] **Step 6: Commit**

```bash
git add README.md docs/specs/2026-05-13-dust-plugin-design.md
git commit -m "docs: install instructions; record manual-verification results"
```

---

## Done

What you've built:
- A four-project C# solution with clean module boundaries (algorithm / data / hdt / ui).
- A fully unit-tested dust algorithm covering playset math, golden preference, locked copies, refund window, Standard/Wild flag, and strategy presets — runnable on Linux via `dotnet test`.
- A data layer that fetches HearthstoneJSON with on-disk cache and loads curated `uncraftable.json` / `refund.json`.
- An HDT plugin DLL that reads the live collection via HDT's `RemoteCollection`, runs the algorithm, and renders a WPF panel with sortable rows, strategy switching, Standard/Wild toggles, CSV/JSON export.
- Manual verification against a live Hearthstone collection.

**Maintenance tasks going forward (not part of this plan):**
- On each Hearthstone balance patch, add nerfed cards to `data/refund.json` with `expiresUtc` set to `patch_date + 14d`.
- On each new expansion / set rotation, update `StandardSets.Codes` in `src/DustAdvisor.Data/StandardSets.cs`.
- On each new bundle / Tavern Pass / Reward Track release, add affected card IDs to `data/uncraftable.json`.
