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
                    new DustAdvisor.Data.NeverSuggestRepository());

                var data = await loader.LoadAsync(
                    locale: locale,
                    uncraftablePath: PluginPaths.UncraftableFile,
                    refundPath: PluginPaths.RefundFile,
                    neverSuggestPath: PluginPaths.NeverSuggestFile,
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

                int matched = 0;
                var metaIds = new System.Collections.Generic.HashSet<string>();
                foreach (var m in data.Meta) metaIds.Add(m.CardId);
                foreach (var c in collection) if (metaIds.Contains(c.CardId)) matched++;

                var deckUsage = BuildDeckUsage();

                Func<DustAdvisor.Algorithm.Domain.AdvisorOptions, DustAdvisor.Algorithm.Domain.DustPlan> recompute = opts =>
                {
                    var ins = new DustAdvisor.Algorithm.AdvisorInputs(
                        collection, data.Meta, data.Uncraftable, data.RefundWindow, opts, deckUsage);
                    return new DustAdvisor.Algorithm.Advisor().Recommend(ins);
                };
                var initialPlan = recompute(new DustAdvisor.Algorithm.Domain.AdvisorOptions());
                var artCache = new DustAdvisor.Ui.Export.CardArtCache(
                    new DustAdvisor.Ui.HttpBinaryFetcher(),
                    PluginPaths.CardArtDir,
                    locale: locale);
                var win = new DustAdvisor.Ui.DustAdvisorWindow(initialPlan, recompute, artCache, PluginPaths.NeverSuggestFile);
                win.Title = $"Dust Advisor — {collection.Count} cards read, {matched} matched metadata";
                win.Show();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString(), "Dust Advisor: error");
            }
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
