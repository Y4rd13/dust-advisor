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
            MenuItem.Click += async (s, e) => await RunAsync();
        }

        public void OnUnload() { }
        public async void OnButtonPress() => await RunAsync();
        public void OnUpdate() { }

        private async System.Threading.Tasks.Task RunAsync()
        {
            try
            {
                string locale = ReadHdtLocaleOrDefault();
                var http = new DustAdvisor.Data.CachingHttpFetcher(
                    new DustAdvisor.Data.HttpClientFetcher(new System.Net.Http.HttpClient()),
                    PluginPaths.CacheDir);
                var hsj = new DustAdvisor.Data.HearthstoneJsonClient(http);
                var loader = new DustAdvisor.Data.DataLoader(
                    hsj,
                    new DustAdvisor.Data.UncraftableRepository(),
                    new DustAdvisor.Data.RefundRepository(),
                    new DustAdvisor.Data.NeverSuggestRepository(),
                    new DustAdvisor.Data.MetaTierRepository());

                var data = await loader.LoadAsync(
                    locale: locale,
                    uncraftablePath: PluginPaths.UncraftableFile,
                    refundPath: PluginPaths.RefundFile,
                    neverSuggestPath: PluginPaths.NeverSuggestFile,
                    metaTiersPath: PluginPaths.MetaTiersFile,
                    now: System.DateTimeOffset.UtcNow,
                    ct: System.Threading.CancellationToken.None);

                var collection = await CollectionSnapshotReader.ReadAsync(data.Meta);

                if (collection.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "Could not read collection from Hearthstone.\n\n" +
                        "Make sure:\n" +
                        "  1. Hearthstone is running and you are logged in.\n" +
                        "  2. You opened the in-game Collection screen at least once this session.\n" +
                        "  3. HDT shows the game as connected (bottom-left of the main HDT window).\n\n" +
                        "If the HDT log at %APPDATA%\\HearthstoneDeckTracker\\Logs\\ shows " +
                        "ScryMemoryAccessException errors, HearthMirror is failing to read the " +
                        "Hearthstone process. Try: close both HDT and Hearthstone, start HDT first, " +
                        "then start Hearthstone, wait at the main menu, open My Collection, then retry.",
                        "Dust Advisor: no collection data");
                    return;
                }

                var metaIds = new System.Collections.Generic.HashSet<string>();
                foreach (var m in data.Meta) metaIds.Add(m.CardId);
                int missingMetaCount = 0;
                foreach (var c in collection) if (!metaIds.Contains(c.CardId)) missingMetaCount++;

                var deckCardIndex = BuildDeckCardIndex();
                var deckStats = BuildDeckStats();

                // Keep the existing deckUsage shape (Dictionary<string,int>) for AdvisorInputs.DeckUsage.
                var deckUsage = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var kv in deckCardIndex) deckUsage[kv.Key] = kv.Value.Count;

                // Compute win-rate per card: sum wins+totals across decks containing the card; threshold 20 games.
                var winRateByCard = new System.Collections.Generic.Dictionary<string, double>();
                foreach (var kv in deckCardIndex)
                {
                    int wins = 0, total = 0;
                    foreach (var deckId in kv.Value)
                    {
                        if (deckStats.TryGetValue(deckId, out var ws))
                        {
                            wins += ws.wins;
                            total += ws.total;
                        }
                    }
                    if (total >= 20)
                        winRateByCard[kv.Key] = (double)wins / total;
                }

                var rotatingCardIds = new System.Collections.Generic.HashSet<string>();
                foreach (var m in data.Meta)
                    if (DustAdvisor.Data.StandardSets.RotatingNextYear.Contains(m.Set.Code))
                        rotatingCardIds.Add(m.CardId);

                Func<DustAdvisor.Algorithm.Domain.AdvisorOptions, DustAdvisor.Algorithm.Domain.DustPlan> recompute = opts =>
                {
                    var ins = new DustAdvisor.Algorithm.AdvisorInputs(
                        collection, data.Meta, data.Uncraftable, data.RefundWindow, opts, deckUsage, rotatingCardIds, data.MetaTiers, winRateByCard);
                    var plan = new DustAdvisor.Algorithm.Advisor().Recommend(ins);
                    if (missingMetaCount > 0)
                    {
                        var warnings = new System.Collections.Generic.List<DustAdvisor.Algorithm.Domain.Warning>(plan.Warnings);
                        warnings.Add(new DustAdvisor.Algorithm.Domain.Warning("__metadata__",
                            $"{missingMetaCount} cards in your collection have no metadata yet (recently added; not in HearthstoneJSON cache)."));
                        plan = new DustAdvisor.Algorithm.Domain.DustPlan(plan.Items, warnings, plan.TotalDust);
                    }
                    return plan;
                };
                var initialPlan = recompute(new DustAdvisor.Algorithm.Domain.AdvisorOptions());
                var artCache = new DustAdvisor.Ui.Export.CardArtCache(
                    new DustAdvisor.Ui.HttpBinaryFetcher(),
                    PluginPaths.CardArtDir,
                    locale: locale);
                var win = new DustAdvisor.Ui.DustAdvisorWindow(initialPlan, recompute, artCache, PluginPaths.NeverSuggestFile);
                win.Title = missingMetaCount > 0
                    ? $"Dust Advisor — {collection.Count} cards read ({missingMetaCount} missing metadata)"
                    : $"Dust Advisor — {collection.Count} cards read";
                win.Show();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString(), "Dust Advisor: error");
            }
        }

        private static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<System.Guid>> BuildDeckCardIndex()
        {
            var index = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<System.Guid>>();
            try
            {
                var decks = Hearthstone_Deck_Tracker.DeckList.Instance.Decks;
                if (decks == null) return Wrap(index);
                foreach (var deck in decks)
                {
                    if (deck == null) continue;
                    if (deck.IsArenaDeck) continue;
                    if (deck.Archived) continue;
                    var version = deck.GetSelectedDeckVersion();
                    if (version?.Cards == null) continue;
                    foreach (var card in version.Cards)
                    {
                        if (string.IsNullOrEmpty(card.Id)) continue;
                        if (!index.TryGetValue(card.Id, out var list))
                        {
                            list = new System.Collections.Generic.List<System.Guid>();
                            index[card.Id] = list;
                        }
                        if (!list.Contains(deck.DeckId)) list.Add(deck.DeckId);
                    }
                }
            }
            catch
            {
                // fall through to empty index
            }
            return Wrap(index);
        }

        private static System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<System.Guid>> Wrap(
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<System.Guid>> source)
        {
            var result = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<System.Guid>>(source.Count);
            foreach (var kv in source) result[kv.Key] = kv.Value;
            return result;
        }

        private static System.Collections.Generic.IReadOnlyDictionary<System.Guid, (int wins, int total)> BuildDeckStats()
        {
            var stats = new System.Collections.Generic.Dictionary<System.Guid, (int wins, int total)>();
            try
            {
                var allStats = Hearthstone_Deck_Tracker.Stats.DeckStatsList.Instance.DeckStats;
                if (allStats == null) return stats;
                // DeckStats is Dictionary<Guid, DeckStats> — iterate values
                foreach (var ds in allStats.Values)
                {
                    if (ds == null) continue;
                    if (ds.Games == null) continue;
                    int wins = 0, total = 0;
                    foreach (var g in ds.Games)
                    {
                        total++;
                        if (g.Result == Hearthstone_Deck_Tracker.Enums.GameResult.Win) wins++;
                    }
                    stats[ds.DeckId] = (wins, total);
                }
            }
            catch
            {
                // fall through to empty stats
            }
            return stats;
        }

        private static System.Collections.Generic.IReadOnlyDictionary<string, int> BuildDeckUsage()
        {
            var usage = new System.Collections.Generic.Dictionary<string, int>();
            try
            {
                var decks = Hearthstone_Deck_Tracker.DeckList.Instance.Decks;
                if (decks == null) return usage;
                foreach (var deck in decks)
                {
                    if (deck == null) continue;
                    if (deck.IsArenaDeck) continue;
                    if (deck.Archived) continue;
                    var version = deck.GetSelectedDeckVersion();
                    if (version?.Cards == null) continue;
                    foreach (var card in version.Cards)
                    {
                        if (string.IsNullOrEmpty(card.Id)) continue;
                        usage.TryGetValue(card.Id, out int n);
                        usage[card.Id] = n + 1;
                    }
                }
            }
            catch
            {
                // If HDT's DeckList API changes shape, fall back to no deck cross-reference.
            }
            return usage;
        }

        private static string ReadHdtLocaleOrDefault()
        {
            try
            {
                var lang = Hearthstone_Deck_Tracker.Helper.GetCardLanguage();
                if (string.IsNullOrEmpty(lang)) return "enUS";
                // HearthstoneJSON locale codes: enUS, deDE, esES, esMX, frFR, itIT, jaJP, koKR,
                // plPL, ptBR, ruRU, thTH, zhCN, zhTW.
                var allowed = new System.Collections.Generic.HashSet<string>
                {
                    "enUS","deDE","esES","esMX","frFR","itIT","jaJP","koKR",
                    "plPL","ptBR","ruRU","thTH","zhCN","zhTW"
                };
                return allowed.Contains(lang) ? lang : "enUS";
            }
            catch
            {
                return "enUS";
            }
        }
    }
}
