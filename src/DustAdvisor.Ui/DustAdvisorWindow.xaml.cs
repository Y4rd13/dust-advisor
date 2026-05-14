using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Ui
{
    public partial class DustAdvisorWindow : Window
    {
        public DustAdvisorWindow(DustPlan plan)
        {
            InitializeComponent();
            Render(plan);
        }

        public void Render(DustPlan plan)
        {
            TotalDustLabel.Text = $"{plan.TotalDust:n0} dust";
            WarningCountLabel.Text = plan.Warnings.Count > 0 ? $"{plan.Warnings.Count} warning(s)" : string.Empty;
            ItemsGrid.ItemsSource = plan.Items.Select(i => new DustItemRow(i)).ToList();
        }
    }
}
