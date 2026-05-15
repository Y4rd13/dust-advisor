# Hearthstone Dust Advisor

A [Hearthstone Deck Tracker (HDT)](https://hsdecktracker.net/) plugin that reads
your live Hearthstone collection and recommends which cards are safe to
disenchant — keeping your playable playset, excluding uncraftable cards, and
flagging refund-window opportunities after balance patches.

The plugin **never disenchants for you**. It only produces a recommended plan
in a separate window; you must dust manually in-game.

## Features

- **Live collection** read via HearthMirror (HDT) — no manual export.
- **Safe playset preservation** — never recommends dusting your last playable copy.
- **Refund-window awareness** — uses full craft cost when a card was recently nerfed (14-day window).
- **Rotation-aware** — separate strategy to dust cards rotating to Wild next.
- **Uncraftable detection** — ~777 cards the game won't let you dust are filtered out automatically (Catch-Up Pack legendaries, Reward Track rewards, achievements, BlizzCon goldens, class basics, etc.).
- **Dark theme** matching HDT's aesthetic.
- **Bilingual UI** — auto-detects your Hearthstone client language (English or Spanish). Other locales fall back to English.
- **Sortable, filterable DataGrid** — by class, rarity, format, premium tier, free-text search.
- **CSV / JSON export** of the current plan.
- **Confirmation cart** with session ledger and undo/redo.
- **Optional per-card meta tier and personal win-rate** columns when you maintain `data/meta_tiers.json` and have enough HDT game history.

## Requirements

- Windows.
- [Hearthstone Deck Tracker](https://hsdecktracker.net/) v1.52.6 or compatible installed.
- [Hearthstone](https://hearthstone.blizzard.com/) installed; you must reach the in-game Collection screen at least once per session so HearthMirror has fresh collection data.

## Install

1. Download the latest release ZIP from the
   [Releases page](../../releases/latest).
2. Extract it. You should get a `DustAdvisor` folder containing six DLLs and a
   `data/` directory.
3. Copy the entire `DustAdvisor` folder into HDT's plugin directory:
   `%APPDATA%\HearthstoneDeckTracker\Plugins\`.
   In Windows Explorer, paste `%APPDATA%\HearthstoneDeckTracker\Plugins` into
   the address bar and drop the folder there.
4. Fully exit HDT (system tray → Exit — closing the window only minimizes it).
5. Reopen HDT. Open **Options → Tracker → Plugins**. You should see
   **Dust Advisor**. Check the **Enable** box.
6. A new **Dust Advisor** entry appears in HDT's menu. Click it.

> **Heads-up:** HDT rewrites `plugins.xml` whenever it detects a plugin DLL
> change. After updating the plugin, you may have to re-enable Dust Advisor in
> the Plugins screen on the next start.

## Usage

1. Launch HDT first, then Hearthstone.
2. Wait at the Hearthstone main menu, then open **My Collection** once so
   HearthMirror sees the data.
3. Click **HDT → Dust Advisor**. The plan window opens, showing total dust,
   per-card recommendations, and warnings.
4. Open **? (How it works)** in the top-right of the window for an in-depth
   explanation of strategies, flags, columns, and shortcuts.
5. Tick the checkbox of every card you intend to dust → click **Confirm** to
   add it to your session ledger → disenchant manually in-game.

### Strategies

| Strategy | What it shows |
|---|---|
| **SafeOnly** *(default)* | Extra copies above your playset. Skips Standard-legal cards by default. |
| **SafeOnlyUnused** | Like SafeOnly, but also skips cards present in any of your HDT decks. |
| **RotationImminent** | Only cards from sets rotating to Wild at the next annual rotation. |
| **MaxDust** | Inverts Golden vs Regular preference to maximize dust (you lose Goldens). |
| **RefundOnly** | Only cards inside the 14-day refund window after a balance patch. |

### Dust values

| Rarity     | Regular | Golden | Craft Reg | Craft Gold |
|------------|---------|--------|-----------|------------|
| Common     |  5      |  50    |  40       |  400       |
| Rare       | 20      | 100    | 100       |  800       |
| Epic       | 100     | 400    | 400       | 1600       |
| Legendary  | 400     | 1600   | 1600      | 3200       |

## Language support

The plugin auto-detects the language from HDT's
`Helper.GetCardLanguage()` (which mirrors your Hearthstone client language):

- Any `es*` locale → Spanish UI.
- Anything else (`enUS`, `frFR`, `deDE`, `jaJP`, …) → English UI.

Card names themselves are pulled from
[HearthstoneJSON](https://api.hearthstonejson.com/) in your actual client
locale, so a French client shows French card names with the English plugin UI.

If you want a translation for another language, open an issue or a PR adding a
`Strings.<lang>.xaml` to `src/DustAdvisor.Ui/Themes/`.

## Build from source

You need the .NET SDK (8.0 or newer). On Windows, .NET Framework 4.8 targeting
pack is also required (installed alongside the SDK or via Visual Studio).

```bash
git clone https://github.com/<your-fork>/dust-advisor
cd dust-advisor
dotnet build src/DustAdvisor.sln -c Release
dotnet test  src/DustAdvisor.sln -c Release --no-build
```

Build outputs land in `src/DustAdvisor.Hdt/bin/Release/net48/`. To run them in
HDT, copy those DLLs (`DustAdvisor*.dll`, `Newtonsoft.Json.dll`) plus the `data/`
folder into `%APPDATA%\HearthstoneDeckTracker\Plugins\DustAdvisor\`.

The repo vendors the two HDT reference assemblies it needs to compile
(`lib/hdt/HearthstoneDeckTracker.exe` and `HearthMirror.dll`, both MIT). If you
have HDT installed locally, the build picks up your installed copy first; the
vendored fallback only kicks in for fresh clones / CI.

## Project layout

```
src/
  DustAdvisor.Algorithm/         pure domain + recommendation engine (netstandard2.0)
  DustAdvisor.Data/              HearthstoneJSON client, repositories, heuristics
  DustAdvisor.Ui/                WPF window, dark theme, i18n
  DustAdvisor.Ui.Export/         CSV / JSON export, card-art cache
  DustAdvisor.Hdt/               IPlugin entry point (net48)
  DustAdvisor.{...}.Tests/       xUnit + FluentAssertions
data/
  meta_tiers.json                user-curated S/A/B/C tier classifications
  refund.json                    cards in 14-day refund window after balance patches
  uncraftable.json               manual override list (heuristic handles most)
lib/hdt/                         vendored HDT reference assemblies (MIT)
```

## Maintenance

- **After a Hearthstone balance patch:** add nerfed card IDs to `data/refund.json`
  with `expiresUtc = patch_date + 14d`. Rebuild and redeploy.
- **At set rotation (annual):** update `StandardSets` in
  `src/DustAdvisor.Data/StandardSets.cs` with the new Standard set codes.
- **On Tavern Pass / Reward Track expansions:** if a new uncraftable-card
  pattern shows up, extend `UncraftablePhrases` in
  `src/DustAdvisor.Data/UncraftableHeuristic.cs` after validating with
  `cards.collectible.json`.
- **On HDT updates:** if `Helper.GetCardLanguage()` or HearthMirror's API
  changes shape, update the calls in `DustAdvisor.Hdt/Plugin.cs` and refresh
  the vendored DLLs under `lib/hdt/`.

## Contributing

Conventional Commits are required — semantic-release uses them to compute
release versions and changelog entries:

- `feat(scope): ...` → minor bump.
- `fix(scope): ...` → patch bump.
- `feat!: ...` or `BREAKING CHANGE:` in body → major bump.
- `chore`, `docs`, `refactor`, `test` → no release.

The repo runs `dotnet build` + `dotnet test` on every push and PR via
`.github/workflows/ci.yml`. Pushes to `main` trigger
`.github/workflows/release.yml`, which builds, tests, packages the plugin into
`DustAdvisor-<version>.zip`, and attaches it to a fresh GitHub release via
[semantic-release](https://github.com/semantic-release/semantic-release).

When adding any user-facing string:

1. Add the key to both `Themes/Strings.en.xaml` and `Themes/Strings.es.xaml`.
2. `LocalizationTests` enforces key parity and matching `{N}` placeholder
   counts. Run `dotnet test` to confirm.

## Disclaimer

This is an unofficial third-party tool. It is **not affiliated with, endorsed
by, or supported by Blizzard Entertainment or HearthSim** (the maintainers of
HDT). All Hearthstone game data, card metadata, and trademarks are property of
their respective owners. Use at your own risk; disenchant decisions are yours.

## License

MIT. See the [LICENSE](LICENSE) file. The vendored HDT reference assemblies
under `lib/hdt/` are MIT-licensed by HearthSim; see `lib/hdt/NOTICE.md` for
attribution.
