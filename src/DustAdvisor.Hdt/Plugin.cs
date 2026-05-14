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
        public void OnButtonPress() { }
        public void OnUpdate() { }

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
    }
}
