# Hearthstone Dust Advisor

Personal HDT plugin (C# / WPF) that reads your Hearthstone collection via HearthMirror and recommends which cards are safe to disenchant. Refund-window aware, rotation aware. Local use only.

- Design spec: `docs/specs/2026-05-13-dust-plugin-design.md`
- Implementation plan: `docs/plans/2026-05-13-dust-plugin.md`

## Install (Windows)

1. Make sure HDT is installed. Path on this machine: `C:\Users\dharm\AppData\Local\HearthstoneDeckTracker\app-1.52.6\`. If your HDT version is different, edit `src/DustAdvisor.Hdt/DustAdvisor.Hdt.csproj` and update the `HdtAppDir` default, then rebuild.

2. Copy the plugin files into HDT's user plugin directory:

   ```cmd
   xcopy /E /I /Y "G:\Documentos\Projects\hearthstone\dist\DustAdvisor" "%APPDATA%\HearthstoneDeckTracker\Plugins\DustAdvisor"
   ```

   (From WSL: `cp -r /mnt/g/Documentos/Projects/hearthstone/dist/DustAdvisor /mnt/c/Users/dharm/AppData/Roaming/HearthstoneDeckTracker/Plugins/`)

3. Open HDT. Go to **Options → Tracker → Plugins**. You should see "Dust Advisor v0.1.0". Enable it.

4. The HDT menu now has a "Dust Advisor" entry. Click it.

## First-run smoke test

1. Launch Hearthstone. Wait for the main menu.
2. Navigate to **My Collection** (the in-game collection manager). This triggers HDT to read the collection via HearthMirror.
3. With Hearthstone still open, click HDT's **Dust Advisor** menu item.
4. A WPF window opens with a list of cards safe to disenchant, total dust at the top, and warnings if any Standard-legal cards are involved.
5. Sanity checks:
   - **Total dust** at the top should be a reasonable number (your accumulated extra copies × disenchant value).
   - **Pick 3 random rows**. For each, find the card in the in-game collection, click Disenchant. The per-copy dust offered by Hearthstone should match `DustGained / (RegularToDust + GoldenToDust * goldenRatio)` for that row — or just check that per-copy values are 5/20/100/400 for Common/Rare/Epic/Legendary regular, 50/100/400/1600 golden, OR full craft cost if the card is flagged REFUND.
   - **Cancel each disenchant** before confirming, unless you really want to dust.
   - **Try the Strategy dropdown**: switching to MaxDust should add Standard-legal cards to the plan; switching to RefundOnly should leave only refund-window cards.
   - **Try Export CSV**: save to disk, open the file, check rows match the grid.

## Maintenance

- **On each Hearthstone balance patch**: add nerfed cards to `data/refund.json` with `expiresUtc = patch_date + 14d`. Rebuild and redeploy.
- **On set rotation (annual)**: update `src/DustAdvisor.Data/StandardSets.cs` with the new Standard set codes. Rebuild and redeploy.
- **On Tavern Pass / Reward Track releases**: add affected card IDs (with their Premium tier) to `data/uncraftable.json`. Rebuild and redeploy.
- **On HDT update**: HDT installs to a new `app-X.Y.Z` directory. Update `HdtAppDir` in the csproj and rebuild.

## Build from source

From WSL:

```bash
cd /mnt/g/Documentos/Projects/hearthstone
'/mnt/c/Program Files/dotnet/dotnet.exe' build "$(wslpath -w src/DustAdvisor.sln)" -c Release
'/mnt/c/Program Files/dotnet/dotnet.exe' test "$(wslpath -w src/DustAdvisor.sln)"
```

42 tests should pass.

## Architecture

Four modules. The algorithm and data layers are pure `netstandard2.0` so they're testable cross-platform. The HDT plugin and WPF window are `net48` (Windows-only):

- `DustAdvisor.Algorithm` — playset math, rarity rules, refund pricing, strategies, sorting. No I/O.
- `DustAdvisor.Data` — HearthstoneJSON fetch with on-disk cache, curated uncraftable + refund file loaders.
- `DustAdvisor.Hdt` — `IPlugin` entry point that bridges HDT's `HearthMirror.Reflection.GetFullCollection()` to `CollectionEntry[]`.
- `DustAdvisor.Ui` — WPF window with Strategy/KeepStandardLegal controls, sortable DataGrid, CSV/JSON export.

See the design spec for full details.
