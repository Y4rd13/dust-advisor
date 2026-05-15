# Dust Advisor — agent guide

Hearthstone Deck Tracker (HDT) plugin (C#/WPF, Windows-only). Recommends which collected cards are safe to disenchant. Public, MIT-licensed; releases ship as ZIPs attached to GitHub releases via semantic-release. End users do **not** build from source — they install from the release ZIP.

## Stack

- C# .NET multi-targeting per project:
  - `DustAdvisor.Algorithm`, `DustAdvisor.Data`, `DustAdvisor.Ui.Export` → `netstandard2.0` (portable)
  - `DustAdvisor.Ui` → `net48` (WPF)
  - `DustAdvisor.Hdt` → `net48` (plugin entry, references HDT assemblies)
  - `*.Tests` → `net8.0` + xUnit + FluentAssertions (~111 tests across 3 suites)
- WPF with `INotifyPropertyChanged`, dark theme via `Themes/DarkTheme.xaml`
- Bilingual UI (EN/ES) via `Themes/Strings.{en,es}.xaml` ResourceDictionaries, language auto-detected from HDT's `Helper.GetCardLanguage()`
- HearthstoneJSON CDN for card metadata
- HearthMirror via HDT's `CollectionHelpers.Hearthstone.GetCollection()` for live collection (ScryDotNet RPC under the hood)

## Project layout

```
src/
  DustAdvisor.sln                        ← solution root (always pass this to dotnet)
  DustAdvisor.Algorithm/                 ← pure domain + recommendation engine
  DustAdvisor.Data/                      ← HearthstoneJSON client, repositories, heuristics
  DustAdvisor.Ui/                        ← WPF window + theme + Localization.cs + Themes/Strings.{en,es}.xaml
  DustAdvisor.Ui.Export/                 ← CSV/JSON export, card-art cache
  DustAdvisor.Hdt/                       ← IPlugin entry (Plugin.cs), collection reader
  DustAdvisor.{Algorithm,Data,Ui}.Tests/ ← xUnit suites
data/
  meta_tiers.json                        ← user-curated S/A/B/C tiers (currently empty stub)
  refund.json                            ← cards in refund window (manual after balance patches)
  uncraftable.json                       ← manual override list (heuristic handles most)
lib/hdt/                                 ← vendored HDT reference DLLs (MIT) used by CI / fresh clones
dist/DustAdvisor/                        ← local development output (NOT committed by releases; CI builds its own zip)
docs/{specs,plans}/                      ← spec + plan docs per version
.github/workflows/                       ← ci.yml (PR validation) + release.yml (semantic-release on main)
```

## Build & deploy

The csproj reference resolution prefers your local HDT install (`%LOCALAPPDATA%\HearthstoneDeckTracker\app-1.52.6`) and falls back to `lib/hdt/` when the local install isn't found. CI always uses the vendored copy.

**Always run an explicit `dotnet build` before deploying locally.** `dotnet test` does NOT rebuild `DustAdvisor.Hdt` (no test project references it), so deploying after test alone ships stale DLLs. This burned a debug cycle once — the user reported "fix doesn't work" and I had to grep the deployed DLL to find the binary was old.

```bash
# Build (Windows dotnet from WSL — paths must be Windows-style)
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" -c Release

# Test
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w /mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.sln)" -c Release --no-build

# Local deploy — copy to BOTH Local AND Roaming plugin dirs (HDT scans both)
DLLS='DustAdvisor.dll DustAdvisor.Algorithm.dll DustAdvisor.Data.dll DustAdvisor.Ui.dll DustAdvisor.Ui.Export.dll Newtonsoft.Json.dll'
SRC=/mnt/g/Documentos/Projects/hearthstone/src/DustAdvisor.Hdt/bin/Release/net48
for f in $DLLS; do
  cp "$SRC/$f" /mnt/g/Documentos/Projects/hearthstone/dist/DustAdvisor/
  cp "$SRC/$f" "/mnt/c/Users/dharm/AppData/Roaming/HearthstoneDeckTracker/Plugins/DustAdvisor/"
  cp "$SRC/$f" "/mnt/c/Users/dharm/AppData/Local/HearthstoneDeckTracker/app-1.52.6/Plugins/DustAdvisor/"
done
```

**Verify the deployed DLL actually contains your changes** (strings in .NET DLLs are UTF-16 LE — plain `strings` won't find them):

```bash
strings -el "/mnt/c/Users/dharm/AppData/Roaming/HearthstoneDeckTracker/Plugins/DustAdvisor/DustAdvisor.Data.dll" | grep -i "<new-phrase-you-added>"
```

## After every deploy: re-enable the plugin

HDT detects DLL changes and **regenerates `plugins.xml` with all plugins set to `IsEnabled=false`** on next startup (defensive default). The user reports "the plugins section is empty / Dust Advisor disappeared." The plugin DID load — it's just disabled in the manifest.

```bash
# Set IsEnabled=true for Dust Advisor in the manifest
PLUGINS_XML="/mnt/c/Users/dharm/AppData/Roaming/HearthstoneDeckTracker/plugins.xml"
# Use Edit tool to flip <IsEnabled>false</IsEnabled> → true for the DustAdvisor entry
```

Then tell the user to fully exit HDT (system tray → Exit, not just the window X — HDT stays resident) and reopen.

## Release flow (CI / semantic-release)

- **Push to `main`** triggers `.github/workflows/release.yml` on `windows-latest`. The workflow builds, runs tests, stages DLLs+`data/` into `release/DustAdvisor.zip`, then runs `semantic-release`.
- semantic-release reads commits since the last tag, computes the next version from Conventional Commit prefixes, generates release notes + CHANGELOG.md, creates a Git tag, opens a GitHub Release, and uploads the zip as `DustAdvisor-v<version>.zip`.
- **PRs run `.github/workflows/ci.yml`** which is build + test only (no release).
- **Commit conventions** drive versioning: `feat:` minor, `fix:` patch, `feat!:` or `BREAKING CHANGE:` major. `chore`, `docs`, `refactor`, `test` don't bump.

**Never push directly to `main`** without explicit user consent — every push triggers a release attempt. Default working branch is `feat/dust-advisor-mvp`; user merges to main when ready to release.

## Domain rules

**Disenchant values per rarity** (`src/DustAdvisor.Algorithm/Constants.cs`):
| Rarity | Regular DE | Golden DE | Regular Craft | Golden Craft |
|--------|-----------|-----------|---------------|--------------|
| Common | 5 | 50 | 40 | 400 |
| Rare | 20 | 100 | 100 | 800 |
| Epic | 100 | 400 | 400 | 1600 |
| Legendary | 400 | 1600 | 1600 | 3200 |

**Playset size:** Legendary = 1, all others = 2.

**Premium tier counts (HDT `int[]` from `GetCollection().Cards[dbfId]`):**
- `[0]` Standard, `[1]` Golden, `[2]` Diamond, `[3]` Signature — owned counts (mutually exclusive per copy)
- `[4..7]` same order — trial copies (loaned/seasonal, must NOT be counted as owned)

**Uncraftable detection:** `UncraftableHeuristic.cs` matches `howToEarn`/`howToEarnGolden` substrings (case-insensitive). Validated against full `cards.collectible.json` — current heuristic catches 777 cards with **zero false positives** on adventure cards (which ARE disenchantable). When adding a new phrase, **always validate** with:

```bash
curl -s https://api.hearthstonejson.com/v1/latest/enUS/cards.collectible.json > /tmp/hs.json
jq -r '.[] | select(.howToEarn != null and (.howToEarn | ascii_downcase | contains("NEW"))) | "\(.id)\t\(.name)\t\(.rarity)\t\(.set)"' /tmp/hs.json
```

…then sample-check that every match is genuinely uncraftable before committing.

## Conventions

- **TDD**: write the failing xUnit test first, then the implementation. FluentAssertions.
- **Conventional Commits required.** semantic-release reads commits to bump version (`feat:` minor, `fix:` patch, breaking flags major). Non-conventional commits don't trigger a release.
- **i18n key parity.** Every user-facing string lives in BOTH `src/DustAdvisor.Ui/Themes/Strings.en.xaml` AND `Strings.es.xaml`. `LocalizationTests` enforces:
  - Same set of `x:Key` values in both files.
  - Same number of `{N}` placeholders per key.
  - Critical keys (title, error caption, help title) are non-empty in both.
  Adding a string anywhere in the plugin means adding both EN and ES entries — `dotnet test` fails otherwise.
- **No backward-compat shims**: delete dead code, don't keep deprecated paths.
- **No new abstractions before a second concrete use case**. Three similar lines beat a premature interface.
- **Commit signing**: inline via git commit flags, never touch global git config.
- **Never push to `main` without explicit user consent.** Each push to main fires the release workflow.

## Common gotchas

1. **`dotnet test` ≠ `dotnet build` for `.Hdt`** — see Build section.
2. **HDT disables your plugin after every deploy** — see "After every deploy" section.
3. **`Foreground` defaults in WPF dark theme** — when adding a new `ComboBox`/`DropDown`/etc., it inherits OS theme colors and renders invisible. Set explicit `Foreground` from `DarkTheme.xaml` resources or use a full `ControlTemplate`.
4. **`Hearthstone_Deck_Tracker.Config.Instance.SelectedLanguage` is obsolete** — use `Helper.GetCardLanguage()` instead.
5. **`DeckStatsList.Instance.DeckStats` is `Dictionary<Guid, DeckStats>`** (not List) — iterate `.Values`.
6. **`HearthMirror.Collection.Cards` value is `int[8]`** with trial counts at indices 4-7 that must be filtered out.
7. **Card "Colifero el Artista" (TOY_703)** and similar Catch-Up Pack legendaries have `howToEarn: "Earnable after opening a {expansion} card pack"` — these are NOT disenchantable. If a user reports a specific card showing up that shouldn't, search by Spanish name in `esES` locale of the API or by id pattern.
8. **Uncraftable heuristic MUST fetch `enUS` regardless of user locale.** HearthstoneJSON localizes `howToEarn` text per locale ("Earnable" → "Se puede conseguir" → "Erhältlich"), so matching English phrases against esES/deDE/etc. produces zero hits — silent failure with no error. `HearthstoneJsonClient.LoadHeuristicUncraftableAsync` hardcodes `"enUS"` for the fetch and ignores its locale param. `LoadCollectibleAsync` keeps user locale for display names. When clearing the cache after a heuristic change, delete `cache/*.json` from BOTH plugin paths.
9. **i18n adds a hard contract.** Adding any new string requires touching both Strings.en.xaml AND Strings.es.xaml. If `LocalizationTests` fails after a feature, you forgot one of them. New format keys (suffix `_Fmt`) must use the same `{0}`/`{1}`/… placeholders in both languages — the parity test verifies placeholder counts match.
10. **Vendored HDT DLLs at `lib/hdt/` may go stale.** When HDT releases a new version with API changes, both refresh `lib/hdt/HearthstoneDeckTracker.exe` + `HearthMirror.dll` AND the version reference in `DustAdvisor.Hdt.csproj` (`HdtAppDir` default).

## When the user reports a bug

1. **Don't claim a fix works without verifying empirically.** The build can succeed and tests can pass while the deployed DLL still has the old code (see gotcha #1). After deploy, grep the binary with `strings -el` for any new string literal your change added.
2. **Read the HDT log** before speculating. `hdt_log.txt` will show `PluginManager.GetModule >> Error loading ...` if the DLL itself failed to load. Absence of errors means it loaded — issue is logic or manifest.
3. **For "card X appears that shouldn't be dustable"**, fetch its HearthstoneJSON entry (`jq '.[] | select(.id == "...")' /tmp/hs.json`) and check `howToEarn` / `howToEarnGolden` against `UncraftableHeuristic.UncraftablePhrases`. If no phrase matches but the card IS uncraftable, add the new phrase (with validation) and a test row.
4. **For "Spanish/English text wrong"**, check both `Themes/Strings.en.xaml` and `Themes/Strings.es.xaml` — the live string is whatever `Helper.GetCardLanguage()` resolved to. Run `LocalizationTests` to confirm both dictionaries have the key.
