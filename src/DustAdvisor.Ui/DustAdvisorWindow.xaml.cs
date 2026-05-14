using System;
using System.Linq;
using System.Windows;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Ui
{
    public partial class DustAdvisorWindow : Window
    {
        private DustPlan _plan;
        private readonly Func<AdvisorOptions, DustPlan> _recompute;

        public DustAdvisorWindow(DustPlan plan, Func<AdvisorOptions, DustPlan> recompute)
        {
            InitializeComponent();
            _plan = plan;
            _recompute = recompute;
            // RotationImminent is in the enum for future use but currently behaves like SafeOnly. Hide it.
            StrategyBox.ItemsSource = new[] { Strategy.SafeOnly, Strategy.MaxDust, Strategy.RefundOnly };
            StrategyBox.SelectedItem = Strategy.SafeOnly;
            Render(plan);
            StrategyBox.SelectionChanged += (s, e) => Recompute();
            KeepStandardBox.Checked += (s, e) => Recompute();
            KeepStandardBox.Unchecked += (s, e) => Recompute();
            ExportCsvButton.Click += (s, e) => SaveAs("CSV (*.csv)|*.csv", DustAdvisor.Ui.Export.CsvExporter.ToCsv(_plan));
            ExportJsonButton.Click += (s, e) => SaveAs("JSON (*.json)|*.json", DustAdvisor.Ui.Export.JsonExporter.ToJson(_plan));
        }

        public void Render(DustPlan plan)
        {
            TotalDustLabel.Text = $"{plan.TotalDust:n0} dust";
            WarningCountLabel.Text = plan.Warnings.Count > 0 ? $"{plan.Warnings.Count} warning(s)" : string.Empty;
            ItemsGrid.ItemsSource = plan.Items.Select(i => new DustItemRow(i)).ToList();
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
    }
}
