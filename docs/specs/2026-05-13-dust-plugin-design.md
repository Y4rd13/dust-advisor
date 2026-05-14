# Hearthstone Safe-to-Dust Plugin — Design Spec

**Status:** Draft for review
**Date:** 2026-05-13
**Author:** benjaserrau@gmail.com

## 1. Problem

Hearthstone players accumulate excess cards over time: duplicates beyond the 2-per-deck (or 1-per-deck for legendaries) playset, golden versions of cards they already own in normal, cards that have rotated to Wild and they no longer play, and a long tail of cosmetic variants (Signature, Diamond) with non-trivial disenchant rules. Manually deciding what to dust in the in-game collection manager is tedious and error-prone — Blizzard's UI does not expose "you own 5 of this; 3 are safe to dust for X Arcane Dust."

We want an algorithm that ingests a player's full collection and produces a precise, defensible list of cards safe to disenchant, with the total dust gained, surfaced as a live overlay while the game is running.

## 2. Goals

1. Ingest the user's full Hearthstone collection automatically — no manual entry.
2. Compute the maximum **safe** dust the user can claim, where "safe" means: never dust a card the user might want back (Core, locked Signature, Diamond, Standard staple, etc.).
3. Present the recommendation as an overlay panel while Hearthstone is running, plus a CSV/JSON export for offline review.
4. Be **meta-aware**: flag refund-window cards (post-balance-change 14-day full-refund), flag Standard-legal cards before suggesting their disenchant.
5. Ship on Windows for the PC client.

## 3. Non-Goals

- macOS / mobile support (Hearthstone runs there but HDT does not).
- Recommending **what to craft** (different problem, different EV calculus).
- Automating the disenchant action — the user clicks in-game; we only advise. This keeps us read-only and away from Blizzard's automation-tolerance line.
- Battlegrounds cosmetics, Mercenaries, Twist-exclusive economy.
- Real-time meta tier-listing from HSReplay (v1 ships static rotation + refund flags only; meta tiers are a v2 stretch).

## 4. Verified Findings (Why This Architecture)

| Question | Verified answer | Source |
|---|---|---|
| Official Blizzard collection API? | **No.** Only static catalog. No `hs.collection` OAuth scope, no profile API. Not on roadmap. | `develop.battle.net/documentation/hearthstone` |
| How does the ecosystem read collections? | **HearthMirror** — C# library that reflects into Hearthstone's Mono/IL2CPP runtime. Used by HDT, HSReplay, HSTracker. Still works May 2026 (HDT v1.52.6, 2026-05-11). Tolerated by Blizzard. | `github.com/HearthSim/HearthMirror`, `github.com/HearthSim/Hearthstone-Deck-Tracker` |
| Do logs contain the collection? | **No.** `Power.log` = game events. `Achievements.log` = deltas only. No file dumps full state with normal/golden/signature counts. | HearthSim help, hearthstone.gg primer |
| Reference exporter? | `pawl/HearthstoneCollectionExporter` — ~200 lines of C# that load HearthMirror.dll + HearthDb.dll and emit `cards.csv`. | `github.com/pawl/HearthstoneCollectionExporter` |
| HDT plugin system? | **Yes.** `IPlugin` interface with `OnLoad/OnUnload/OnUpdate(~100ms)/OnButtonPress`. DLL drop-in to `HDT/Plugins/`. Active ecosystem. | HDT wiki `Creating-Plugins` |
| Pre-existing "safe to dust" plugin? | **Yes**, `CLJunge/Spawn.HDT.DustUtility` (basic, not meta/refund-aware, UI stagnant). HearthDuster (standalone, abandoned since 2017). | GitHub repos |
| Free card metadata? | HearthstoneJSON (`api.hearthstonejson.com/v1/latest/<locale>/cards.collectible.json`). 15 locales. Has rarity, set, dbfId, howToEarn. **Does not** have `dustValue` (compute from rarity) or `uncraftable` flag (curate manually). | `hearthstonejson.com/docs/cards.html` |

## 5. Architecture

```
                ┌──────────────────────────────┐
                │       Hearthstone (Windows)  │
                │   Mono/IL2CPP runtime         │
                └────────────┬─────────────────┘
                             │  reflection
                             ▼
                ┌──────────────────────────────┐
                │   HDT process (HDT.exe)      │
                │   - HearthMirror.dll         │
                │   - WPF transparent overlay  │
                │   - Plugin loader            │
                │   - HDT.Api: GameEvents,     │
                │     DeckManager, Collection  │
                └────────────┬─────────────────┘
                             │  plugin DLL boundary
                             ▼
                ┌──────────────────────────────┐
                │ DustAdvisor.dll  (this proj) │
                │                              │
                │ ┌──────────────┐  ┌────────┐ │
                │ │  Algorithm   │  │  UI    │ │  reads
                │ │  (pure C#)   │  │  WPF   │◀┼───── cards.collectible.json
                │ │  → DustPlan  │  │  panel │ │      (HearthstoneJSON)
                │ └──────────────┘  └────────┘ │
                │ ┌──────────────────────────┐ │  reads
                │ │ uncraftable.json (curated)│◀┼───── data/uncraftable.json (in repo)
                │ │ refund.json (per-patch)  │ │      data/refund.json
                │ └──────────────────────────┘ │
                └──────────────────────────────┘
```

### 5.1 Module boundaries

- **`DustAdvisor.Algorithm`** — pure, dependency-free C# library. Inputs: `Collection` + `CardMeta[]` + `Uncraftable` + `RefundCards` + `Options`. Output: `DustPlan { items: DustItem[], totalDust: int, warnings: Warning[] }`. Fully unit-testable without HDT.
- **`DustAdvisor.Data`** — fetches and caches HearthstoneJSON, loads bundled `uncraftable.json` and `refund.json`. One responsibility: produce normalized `CardMeta`.
- **`DustAdvisor.Hdt`** — the only module that depends on HDT's plugin API. Implements `IPlugin`. Reads collection via `Hearthstone_Deck_Tracker.Hearthstone.RemoteCollection` (HDT's wrapper around HearthMirror), wires the algorithm and the UI, registers a menu item.
- **`DustAdvisor.Ui`** — WPF panel (XAML). Receives a `DustPlan` and a callback to re-run the algorithm with new options.

This split lets us evolve the algorithm independently of HDT and lets us potentially port `Algorithm + Data` to a future macOS/standalone front end with no rewrite.

### 5.2 Lifecycle

1. User installs the plugin (`DustAdvisor.zip` extracted under `HDT/Plugins/DustAdvisor/`).
2. HDT loads `DustAdvisor.dll` at startup; `OnLoad` registers menu item "Dust Advisor".
3. On first run, the data module fetches `cards.collectible.json` from HearthstoneJSON (cached in `%AppData%/HearthstoneDeckTracker/DustAdvisor/cache/`), retries from cache on offline.
4. User opens Hearthstone and navigates to Collection → HDT's collection sync runs.
5. User clicks "Dust Advisor" menu item → UI panel opens, algorithm runs, plan is rendered.
6. User adjusts options (Strategy preset, "include goldens", "include Wild"); algorithm re-runs in-process; UI updates.
7. User clicks Export → CSV/JSON written to user-chosen path.

### 5.3 Threading

- Algorithm runs on a background `Task`. Typical input (~3,000 collectible cards × few copies) finishes in <100 ms. UI uses async/await + binding.
- `OnUpdate` (called ~10x/sec by HDT) does **nothing** for us; we don't need a polling loop. Recomputation is event-driven (UI option change, collection sync completed).

## 6. Dust Algorithm

### 6.1 Constants

```csharp
// Disenchant values (unchanged across all 2025-2026 patches)
DE_REG    = { Common: 5,  Rare: 20,  Epic: 100, Legendary: 400  }
DE_GOLD   = { Common: 50, Rare: 100, Epic: 400, Legendary: 1600 }
CRAFT     = { Common: 40, Rare: 100, Epic: 400, Legendary: 1600 }
PLAYSET   = { non-Legendary: 2, Legendary: 1 }
```

### 6.2 Inputs

- `collection: Map<CardId, { regular: int, golden: int, signature: int, diamond: int }>` — from HDT/HearthMirror.
- `meta: Map<CardId, { rarity, set, isCollectible, howToEarn, ... }>` — from HearthstoneJSON.
- `uncraftable: Set<(CardId, Premium)>` — curated list of locked copies (Reward-Track goldens, Tavern-Pass Signatures, etc.).
- `refundWindow: Set<CardId>` — cards currently within their 14-day post-nerf full-refund window. Maintained per patch in `data/refund.json`.
- `options: { strategy: SafeOnly | MaxDust | RotationImminent | RefundOnly, keepStandardLegal: bool, keepGoldenAsPlayset: bool }`.

### 6.3 Per-card decision

```
for each cardId, counts in collection:
    meta = lookup(cardId)
    if meta is null or not meta.isCollectible: skip                 # filler / non-collectible
    if meta.rarity == FREE: skip                                    # Legacy free
    if meta.set == "CORE": skip                                     # loaned, not owned
    if (cardId, premium) ∈ uncraftable: skip that premium tier
    if premium == Diamond: skip diamonds entirely

    if options.keepStandardLegal and isStandardLegal(meta.set):
        warn(cardId, "Standard-legal; consider keeping. Dust only if you've decided to stop playing this card.")
        if options.strategy == SafeOnly: skip
    if not isStandardLegal(meta.set) and options.strategy == RotationImminent:
        # this card already rotated — encourage dusting
        priorityBoost(cardId)

    playset = (meta.rarity == LEGENDARY ? 1 : 2)

    # Prefer to keep goldens as the playset (cosmetic upgrade), dust regulars first
    keepGold = min(counts.golden, playset)
    keepReg  = max(0, playset - keepGold)
    dustReg  = max(0, counts.regular - keepReg)
    dustGold = max(0, counts.golden  - keepGold)

    deUnitReg  = (cardId ∈ refundWindow) ? CRAFT[rarity]    : DE_REG[rarity]
    deUnitGold = (cardId ∈ refundWindow) ? 4 * CRAFT[rarity] : DE_GOLD[rarity]

    plan.add(cardId, dustReg, dustGold, dustReg*deUnitReg + dustGold*deUnitGold,
             refundFlag=(cardId ∈ refundWindow),
             standardFlag=isStandardLegal(meta.set))
```

### 6.4 Edge cases

1. **Goldens count toward playset.** 2 goldens of a non-legendary = full playset; do not flag missing regular.
2. **Signature & Diamond count toward deck-building playset** but Diamond never dusts, and Signature dusts only if its source isn't on the locked-Signature list.
3. **Uncraftable detection.** Primary source: hand-curated `uncraftable.json` keyed by `(dbfId, premium)`. Heuristic fallback: parse `howToEarn` for "Tavern Pass", "Rewards Track", "Achievement", "Twitch", "Bundle" and treat the matching premium as uncraftable. Heuristic is **secondary**; the curated list wins.
4. **Refund window.** Loaded from `data/refund.json` with `{ cardId, premium, expiresUtc }`. The plugin checks `expiresUtc > now` per session start; expired entries are pruned by maintainer in a follow-up commit.
5. **Reno / Highlander cards.** Deck-building constraint, not collection constraint. Playset is still 1 (they are legendaries).
6. **Marin's treasures, Battleground heroes, Mercenaries** — `isCollectible == false` in HearthstoneJSON or absent from collectible file; skipped automatically.

### 6.5 Strategy presets

| Strategy | Behavior |
|---|---|
| **SafeOnly** ★ default | Only excess copies beyond playset. Skip Standard-legal cards entirely. Skip Wild meta staples (v2, not v1). |
| **MaxDust** | Dust everything beyond playset, including Standard-legal extras. Loud warnings on each. |
| **RotationImminent** | Prioritize cards that rotate at next year-flip (HearthstoneJSON `set` ∈ next-rotation-list). |
| **RefundOnly** | Show only cards in `refundWindow`. Highest EV per copy. |

## 7. UI

### 7.1 Layout

A WPF panel (size ~520×700 px, dockable) with:
- **Header**: total recommended dust (large), strategy dropdown, "Refresh from collection" button.
- **Filters**: rarity checkboxes, "include goldens" toggle, "include Signature (craftable)" toggle, "keep Standard-legal" toggle.
- **List**: sortable table — Card name, Set, Rarity, Owned (R/G/S/D), Dust to Claim (R+G), Dust Value, Flags (REFUND, STANDARD, WILD-ONLY). Multi-select.
- **Footer**: "Export CSV", "Export JSON", "Copy to clipboard".

### 7.2 Visual flags

- 🔴 **REFUND** — refund window. Background highlight.
- 🟡 **STANDARD** — currently Standard-legal. Caution.
- ⚪ **WILD-ONLY** — rotated. Safe-to-dust default.

(No emoji in code — these are visual labels in the UI only.)

### 7.3 Interaction

- Click a row → opens the card on HearthstoneJSON in the system browser for context.
- Select rows → footer updates with selected-only dust total. Export emits only selected rows if any are selected, else full plan.

## 8. Differentiators vs. CLJunge/Spawn.HDT.DustUtility

1. **Refund-window awareness** — Dust Utility ignores post-nerf refund windows. Ours highlights them as red and prioritizes them.
2. **Rotation awareness** — explicit Standard/Wild flags, dedicated "RotationImminent" preset.
3. **Premium handling** — full Signature / Diamond / locked-Signature taxonomy. Dust Utility lumps cosmetic variants.
4. **Uncraftable curation** — bundled `uncraftable.json` that we maintain per-patch. Dust Utility relies only on what HearthMirror exposes.
5. **Export formats** — CSV + JSON for downstream tooling; Dust Utility is screen-only.
6. **Pure-C# algorithm core** — testable without HDT, portable to other front ends later.

## 9. Risks

| Risk | Mitigation |
|---|---|
| Hearthstone patch breaks HearthMirror | HDT updates within hours typically; we depend on HDT's wrapper, so we inherit the fix. Pin to HDT's `RemoteCollection` API, not direct HearthMirror calls. |
| HearthstoneJSON downtime | Cache `cards.collectible.json` to `%AppData%`; fall back to cache. Ship a vendored copy in the release zip as last resort. |
| Blizzard tightens stance on memory-reading | Out of our control. Same risk as HDT itself. Read-only, no input automation. |
| `uncraftable.json` falls out of date | Document update process in `CONTRIBUTING.md`. Subscribe to patch notes feed. Expose "unknown — treat as uncraftable" default. |
| Refund window dates drift | Maintain `refund.json` with explicit `expiresUtc`; plugin auto-prunes expired entries at session start. |
| Duplicates effort vs. Dust Utility | Differentiation listed in §8 is material, not cosmetic. If it isn't, we shouldn't ship. |

## 10. Out of Scope (v1)

- HSReplay meta tier integration (v2).
- Auto-detect Blizzard patch and update `refund.json` (v2 — manual updates ship faster).
- Cross-account collection comparison (v2).
- Localized UI (v1 English; data layer already supports 15 locales for card names).
- A standalone (non-HDT) front end. Algorithm/Data modules are decoupled so this is feasible later without rewrite.

## 11. Open Decisions

1. **Distribution channel**: bundled GitHub Releases zip only, or also submitted to the HDT Plugins wiki? *Default: GitHub Releases first, wiki submission once stable.*
2. **License**: MIT vs. Apache-2.0? *Default: MIT, matching most HDT plugins.*
3. **Telemetry**: none (privacy-first) vs. opt-in anonymous "how much dust does my plugin save" stat? *Default: none in v1.*

## 12. Verification & Acceptance

- Algorithm unit tests covering each edge case in §6.4 with fixture collections.
- Manual integration test: install plugin in HDT, log into Hearthstone, sync collection, click menu item, verify panel renders and Export CSV produces a valid file with rows matching in-game disenchant values for a sample of cards.
- Confirm in-game dust totals match for at least 20 sampled rows (manual cross-check against the in-game disenchant button) before tagging v1.0.

## Sources

- Hearthstone Game Data APIs — https://community.developer.battle.net/documentation/hearthstone/game-data-apis
- HearthMirror — https://github.com/HearthSim/HearthMirror
- Hearthstone Deck Tracker — https://github.com/HearthSim/Hearthstone-Deck-Tracker
- HDT Plugin wiki — https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/Creating-Plugins
- HDT Available Plugins — https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/Available-Plugins
- HearthstoneCollectionExporter (reference) — https://github.com/pawl/HearthstoneCollectionExporter
- Spawn.HDT.DustUtility (predecessor) — https://github.com/CLJunge/Spawn.HDT.DustUtility
- HearthDuster (abandoned reference) — https://github.com/ifeherva/HearthDuster
- HearthstoneJSON — https://hearthstonejson.com/ and https://hearthstonejson.com/docs/cards.html
- Disenchant value table — https://hearthstone.fandom.com/wiki/Crafting
- Uncraftable cards — https://hearthstone.fandom.com/wiki/Uncraftable, https://us.support.blizzard.com/en/article/000195185
- Dust refunds — https://hearthstone.fandom.com/wiki/Dust_refunds
- 2026 Year of the Scarab rotation — https://www.hearthstonetopdecks.com/welcome-to-the-year-of-the-scarab-2026-hearthstone-standard-year-information/
- Deckstring format — https://hearthsim.info/docs/deckstrings/
