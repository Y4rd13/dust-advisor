using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Ui.Export;

namespace DustAdvisor.Ui
{
    public enum FormatFilter
    {
        All,
        Standard,
        Wild,
    }

    public partial class DustAdvisorWindow : Window
    {
        private DustPlan _plan;
        private readonly Func<AdvisorOptions, DustPlan> _recompute;
        private readonly CardArtCache _artCache;
        private bool _ready;

        public DustAdvisorWindow(DustPlan plan, Func<AdvisorOptions, DustPlan> recompute, CardArtCache artCache)
        {
            InitializeComponent();
            _plan = plan;
            _recompute = recompute;
            _artCache = artCache;
            // RotationImminent is in the enum for future use but currently behaves like SafeOnly. Hide it.
            StrategyBox.ItemsSource = new[] { Strategy.SafeOnly, Strategy.MaxDust, Strategy.RefundOnly };
            StrategyBox.SelectedItem = Strategy.SafeOnly;
            FormatBox.ItemsSource = Enum.GetValues(typeof(FormatFilter));
            FormatBox.SelectedItem = FormatFilter.All;
            _ready = true;
            Render(plan);

            StrategyBox.SelectionChanged += (s, e) => Recompute();
            KeepStandardBox.Checked += (s, e) => Recompute();
            KeepStandardBox.Unchecked += (s, e) => Recompute();

            RoutedEventHandler refilter = (s, e) => Render(_plan);
            FormatBox.SelectionChanged += (s, e) => Render(_plan);
            foreach (var box in new[] { RarityCommonBox, RarityRareBox, RarityEpicBox, RarityLegendaryBox, ShowNormalBox, ShowGoldenBox })
            {
                box.Checked += refilter;
                box.Unchecked += refilter;
            }

            TargetDustBox.TextChanged += (s, e) => Render(_plan);

            ExportCsvButton.Click += (s, e) => SaveAs("CSV (*.csv)|*.csv", DustAdvisor.Ui.Export.CsvExporter.ToCsv(BuildFilteredPlan()));
            ExportJsonButton.Click += (s, e) => SaveAs("JSON (*.json)|*.json", DustAdvisor.Ui.Export.JsonExporter.ToJson(BuildFilteredPlan()));
        }

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

        private IEnumerable<DustItem> ApplyFilters(IReadOnlyList<DustItem> items)
        {
            var format = (FormatFilter)FormatBox.SelectedItem;
            var rarities = new HashSet<Rarity>();
            if (RarityCommonBox.IsChecked == true) rarities.Add(Rarity.Common);
            if (RarityRareBox.IsChecked == true) rarities.Add(Rarity.Rare);
            if (RarityEpicBox.IsChecked == true) rarities.Add(Rarity.Epic);
            if (RarityLegendaryBox.IsChecked == true) rarities.Add(Rarity.Legendary);
            bool showNormal = ShowNormalBox.IsChecked == true;
            bool showGolden = ShowGoldenBox.IsChecked == true;

            foreach (var i in items)
            {
                if (!rarities.Contains(i.Rarity)) continue;
                if (format == FormatFilter.Standard && !i.IsStandardLegal) continue;
                if (format == FormatFilter.Wild && i.IsStandardLegal) continue;
                if (!showNormal && i.RegularToDust > 0 && i.GoldenToDust == 0) continue;
                if (!showGolden && i.GoldenToDust > 0 && i.RegularToDust == 0) continue;
                if (!showNormal && !showGolden) continue;
                yield return i;
            }
        }

        private DustPlan BuildFilteredPlan()
        {
            var visible = ApplyFilters(_plan.Items).ToList();
            return new DustPlan(visible, _plan.Warnings, visible.Sum(i => i.DustGained));
        }

        private void Recompute()
        {
            var opts = new AdvisorOptions(
                strategy: (Strategy)StrategyBox.SelectedItem,
                keepStandardLegal: KeepStandardBox.IsChecked == true);
            _plan = _recompute(opts);
            Render(_plan);
        }

        private void SaveAs(string filter, string content)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = filter };
            if (dlg.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(dlg.FileName, content, System.Text.Encoding.UTF8);
            }
        }

        private async void DataGridRow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGridRow row && row.DataContext is DustItemRow item)
            {
                await item.EnsureArtLoadedAsync(_artCache);
            }
        }
    }
}
