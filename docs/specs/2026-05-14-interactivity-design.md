# Dust Advisor Interactivity — Design Spec

**Status:** Draft for review
**Date:** 2026-05-14
**Scope:** Personal/local use only. Extends `v1.0-personal` of the HDT plugin.

## 1. Goal

Turn the dust-advisor window from a static "list of suggestions" into a workflow tool. Three feature packages, designed to compose:

- **A. Visual & Keyboard** — card-art preview on hover/double-click + keyboard navigation and action shortcuts.
- **B. Action Mode** — per-row selection + sticky bulk-confirm + undo stack.
- **C. Memory & Meta** — target-dust input that filters the plan to the minimum painful subset + persistent "never suggest" list per `(cardId, premium)`.

Packages are independent. Each ships as its own commit so we can stop at any point if value is sufficient.

## 2. Non-Goals

- Multi-user sync, server-side persistence, sharing. Personal tool, local files only.
- Writing to game memory (we never modify the user's actual collection — we only advise).
- HSReplay popularity/tier integration. Out of scope for v1.1.
- Card-art download caching strategy beyond a simple `WebClient` + per-card local image cache.
- Multi-region collections. The user has one Battle.net account.

## 3. Package A — Visual & Keyboard

### A.1 Card art preview

Source: HearthstoneJSON CDN at `https://art.hearthstonejson.com/v1/render/latest/{locale}/256x/{cardId}.png` (also `512x` for the big popup). These URLs already return finished card renders, no compositing needed.

UI behavior:
- **Hover** on a `DataGrid` row → small popup (256x) appears at the cursor, follows movement, dismisses on row-leave. Implemented as a `ToolTip` with `PlacementMode="Mouse"`, deferred 300ms so quick scrolls don't spam fetches.
- **Double-click** on a row → modeless detail window with 512x art, full card text, rarity, set, premium counts in your collection, and the dust math for that card. Closes with Escape or click-outside.

Caching: per-cardId image cached in `%APPDATA%\HearthstoneDeckTracker\Plugins\DustAdvisor\cache\art\{cardId}_256.png` (and `_512.png`). On first hover, fetch and cache; subsequent hovers read from disk. The existing `CachingHttpFetcher` pattern is the model.

Failure mode: if the network or CDN fails, the popup shows a placeholder text "(no art available)" and never crashes. The card row still works normally.

### A.2 Keyboard navigation and actions

Bindings, active when the `DataGrid` has focus:

| Key | Action |
|---|---|
| ↑ / ↓ | Move row selection |
| Enter | Open the big preview window for the focused row |
| Space | Toggle the row's selection-cart checkbox (from Package B) |
| D | Mark focused row as "to disenchant" (joins selection cart) and auto-advance ↓ |
| K | Mark focused row as "keep" (adds to Package C's never-suggest list, removes row from grid) and auto-advance |
| Ctrl+Z | Undo the last D/K action |
| Ctrl+Y / Ctrl+Shift+Z | Redo |
| Esc | Clear current selection-cart |

Auto-advance behavior is gated by a checkbox `[ ] Auto-advance after action` in the header — default ON. When OFF, the cursor stays.

### A.3 Status footer

Below the grid, a new always-visible status strip:

```
Session: 12 cards marked → 1,840 dust   |   Filters: 4,210 visible of 4,827 plan
```

The "Session" half is owned by Package B. The "Filters" half is computed from the existing filter chain.

---

## 4. Package B — Action Mode (Selection Cart + Undo)

### B.1 Selection cart

New leftmost column in `DataGrid`: a checkbox per row. Two-way bound to a per-`DustItem` `IsSelected` flag held in a `Dictionary<string, bool>` (cardId → selected) inside the window. This dictionary is in-memory only (cleared when window closes).

The sticky footer appears whenever `IsSelected` count > 0:

```
┌─────────────────────────────────────────────────────────┐
│ 12 cards selected = 1,840 dust   [Confirm]   [Clear]    │
└─────────────────────────────────────────────────────────┘
```

`Confirm` action: logs the confirmed batch to an in-memory session ledger, clears the cart, shows a toast notification ("Marked 12 cards for disenchant (1,840 dust). Undo"). The selection ledger persists for the window's lifetime but does NOT modify the actual game state — the user still has to dust the cards in-game manually. The advisor's job is the recommendation, not the execution.

### B.2 Undo stack

Two stacks: `_undo: Stack<Action>` and `_redo: Stack<Action>`. Each user action (mark/unmark, confirm, "never suggest") pushes an inverse onto `_undo`. `Ctrl+Z` pops from `_undo`, applies, pushes inverse onto `_redo`. `Ctrl+Y` reverses. Toast notification on each undo: "Undid: marked X". Stack depth capped at 100.

### B.3 Toast notifications

Lightweight bottom-right popup using a `Popup` element bound to a `BindingList<string>` of recent messages. Each message auto-fades after 3 seconds. Click the toast → undoes the underlying action.

---

## 5. Package C — Memory & Meta

### C.1 Target-dust input

New text input in the header row: `Target dust: [_____]`. When non-empty and numeric, the grid filters/sorts to a **minimum-pain subset that satisfies the target**.

Algorithm: greedy. Sort plan items by `(IsStandardLegal asc, DustGained desc)` — Wild cards first because they have less opportunity cost than current-meta cards, then highest dust per card first because that minimizes the number of cards the user has to actually disenchant. Walk the sorted list, accumulating dust until the running total ≥ target. Show only those rows.

Display shows `"1,680 / 1,600 target  (4 cards)"`. The user can refine — toggle Premium/Rarity filters to exclude certain cards from the candidate set, and the target-fit recomputes.

If the total achievable dust (plan total) is less than target, show a warning row in red: `"Target exceeds available safe dust by N"`.

### C.2 Persistent "Never suggest" list

Right-click context menu on a row:
```
Open card in browser (HearthstoneJSON)
─────────────────
Never suggest this card (Regular)
Never suggest this card (Golden)
Never suggest this card (any premium)
```

Selection writes to `%APPDATA%\HearthstoneDeckTracker\Plugins\DustAdvisor\never_suggest.json`:

```json
[
  { "cardId": "CORE_AT_001", "premium": "Regular" },
  { "cardId": "EX1_565",     "premium": "Golden" }
]
```

This file is loaded by `DataLoader.LoadAsync` and merged into the same `Uncraftable` set that the algorithm already respects. The user's choice is durable across sessions; the only way to undo is to manually edit the JSON or use Ctrl+Z within the same session.

The "K"-key shortcut from Package A maps to `Never suggest this card (any premium)` by default.

---

## 6. Architecture

### 6.1 Module boundaries

The existing four-project split remains. All changes land in `DustAdvisor.Ui` and `DustAdvisor.Data`:

- `DustAdvisor.Data`:
  - New `NeverSuggestRepository` analogous to `UncraftableRepository`.
  - `DataLoader.LoadAsync` merges three sources into the uncraftable set: curated `uncraftable.json` + heuristic `howToEarn` set + new `never_suggest.json`.
- `DustAdvisor.Ui`:
  - New `CardArtCache` (HearthstoneJSON CDN + local disk cache).
  - New `CardDetailWindow` (modeless WPF for double-click preview).
  - New `SessionLedger` and `UndoStack` plain C# classes.
  - `DustAdvisorWindow` gets the new controls (selection column, target input, footer) and event wiring.

### 6.2 No new tests required for UI behavior

WPF event handling is hard to unit-test. We test:
- `NeverSuggestRepository.Load/Save` round-trips.
- `TargetDustOptimizer` greedy algorithm (pure function).
- `CardArtCache.GetAsync` with a stub HTTP fetcher.

UI smoke tests are manual (open window, click stuff).

### 6.3 Implementation order

The packages are mostly independent, but A.1 and B.1 both modify the `DustAdvisorWindow` constructor. Recommended order:

1. **C.2 first** (persistent never-suggest) — pure data layer, no UI risk, gives immediate value.
2. **C.1** (target dust) — pure algorithm + one text input.
3. **A.1** (card art preview) — async resource fetching, modeless detail window.
4. **B.1 + B.2 + B.3** (selection cart + undo + toasts) — most UI surface, biggest risk.
5. **A.2** (keyboard shortcuts) — last, because it depends on B.1 (the cart) and C.2 (the never-suggest list).
6. **A.3** (status footer) — trivial, last.

Each step is one commit. After step 3 we already have a substantially better tool.

## 7. Risks

| Risk | Mitigation |
|---|---|
| HearthstoneJSON CDN is slow or down | Cache locally on first hit. Placeholder text if both network and cache miss. |
| Card art URLs change format | URL pattern is documented and stable since 2019. If it changes, one constant updates. |
| WPF DataGrid hover ToolTip flicker | Use `ToolTipService.InitialShowDelay="300"` and a single shared `ToolTip` instance bound to the row context. |
| Undo stack memory | Cap at 100 entries. Each action is a small lambda + cardId string — negligible. |
| `never_suggest.json` lost on disk error | Write atomically (temp file + rename). Don't write on every action — debounce 2 seconds. |
| Selection cart "Confirm" misleads user into thinking the plugin dusts cards | Toast message and modal text explicitly say "Marked for your in-game disenchant — the plugin does not modify your collection." |

## 8. Out of Scope (Future)

- Grid-of-card-art view toggle (alt to table). Nice to have.
- HSReplay popularity strategy (`Strategy.Meta` preset).
- Dust history graph (line chart of session-by-session disenchant totals).
- Deck cross-reference: "you can't dust this, it's in 3 of your decks".
- Drag-and-drop reordering.

## Sources

- HearthstoneJSON image API — https://hearthstonejson.com/docs/images.html (URL pattern `https://art.hearthstonejson.com/v1/render/latest/{locale}/256x/{cardId}.png`)
- Spawn.HDT.DustUtility — https://github.com/CLJunge/Spawn.HDT.DustUtility (predecessor patterns)
- HearthDuster — https://github.com/ifeherva/HearthDuster (abandoned reference)
- Aftershoot keyboard-driven culling pattern — https://support.aftershoot.com/en/articles/5227867-aftershoot-keyboard-shortcuts
- PatternFly bulk selection guidelines — https://www.patternfly.org/patterns/bulk-selection/
- WPF ToolTip overview — https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/tooltip-overview
