using System.Linq;
using System.Windows;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Ui
{
    public partial class DustAdvisorWindow : Window
    {
        private readonly DustPlan _plan;

        public DustAdvisorWindow(DustPlan plan)
        {
            InitializeComponent();
            _plan = plan;
            Render(plan);
            ExportCsvButton.Click += (s, e) => SaveAs("CSV (*.csv)|*.csv", DustAdvisor.Ui.Export.CsvExporter.ToCsv(_plan));
            ExportJsonButton.Click += (s, e) => SaveAs("JSON (*.json)|*.json", DustAdvisor.Ui.Export.JsonExporter.ToJson(_plan));
        }

        public void Render(DustPlan plan)
        {
            TotalDustLabel.Text = $"{plan.TotalDust:n0} dust";
            WarningCountLabel.Text = plan.Warnings.Count > 0 ? $"{plan.Warnings.Count} warning(s)" : string.Empty;
            ItemsGrid.ItemsSource = plan.Items.Select(i => new DustItemRow(i)).ToList();
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
