using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        private ToastHost _toasts;
        private readonly DustAdvisor.Ui.Export.SessionLedger _ledger = new DustAdvisor.Ui.Export.SessionLedger();
        private readonly DustAdvisor.Ui.Export.UndoStack _undo = new DustAdvisor.Ui.Export.UndoStack(maxDepth: 100);
        private readonly DustAdvisor.Data.NeverSuggestRepository _neverRepo = new DustAdvisor.Data.NeverSuggestRepository();
        private readonly string _neverSuggestPath;

        public DustAdvisorWindow(DustPlan plan, Func<AdvisorOptions, DustPlan> recompute, CardArtCache artCache, string neverSuggestPath)
        {
            InitializeComponent();
            _plan = plan;
            _recompute = recompute;
            _artCache = artCache;
            _neverSuggestPath = neverSuggestPath;
            _toasts = new ToastHost(ToastContainer);
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

            CompositionTarget.Rendering += (s, e) => UpdateConfirmBar();
            ConfirmButton.Click += (s, e) => ConfirmCart();
            ClearCartButton.Click += (s, e) => ClearCart();

            this.PreviewKeyDown += DustAdvisorWindow_PreviewKeyDown;
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
            StatusFooter.Text = $"Session: {_ledger.Entries.Count} batches → {_ledger.TotalDust:n0} dust   |   Visible: {visible.Count} of {plan.Items.Count} plan rows";
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

        private void DataGridRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGridRow row && row.DataContext is DustItemRow uirow)
            {
                // Find the matching DustItem (the row VM doesn't keep a reference to it).
                var item = _plan.Items.FirstOrDefault(x => x.CardId == uirow.CardId);
                if (item == null) return;
                var win = new CardDetailWindow(item, _artCache) { Owner = this };
                win.Show();
            }
        }

        private void UpdateConfirmBar()
        {
            if (ItemsGrid.ItemsSource is System.Collections.Generic.IEnumerable<DustItemRow> rows)
            {
                int selected = 0;
                int dust = 0;
                foreach (var r in rows)
                {
                    if (r.IsSelected) { selected++; dust += r.DustGained; }
                }
                if (selected > 0)
                {
                    ConfirmText.Text = $"{selected} cards selected = {dust:n0} dust";
                    ConfirmBar.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    ConfirmBar.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }

        private void ConfirmCart()
        {
            var rows = (ItemsGrid.ItemsSource as System.Collections.Generic.IEnumerable<DustItemRow>) ?? new DustItemRow[0];
            var selected = rows.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0) return;
            var dust = selected.Sum(r => r.DustGained);
            var cardIds = selected.Select(r => r.CardId).ToList();
            _ledger.RecordConfirm(dust, cardIds);
            _undo.Push(
                label: $"marked {cardIds.Count} cards",
                undo: () => { _ledger.Undo(); foreach (var r in selected) r.IsSelected = true; });
            foreach (var r in selected) r.IsSelected = false;
            _toasts.Show($"Marked {cardIds.Count} cards for disenchant ({dust:n0} dust)", onClick: () => { _undo.Undo(); });
            Render(_plan);
        }

        private void ClearCart()
        {
            if (ItemsGrid.ItemsSource is System.Collections.Generic.IEnumerable<DustItemRow> rows)
            {
                foreach (var r in rows) r.IsSelected = false;
            }
            Render(_plan);
        }

        private void DataGridRow_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            if (!(sender is System.Windows.Controls.DataGridRow dgRow)) return;
            // Build (or rebuild) the ContextMenu in code-behind so Click handlers wire cleanly.
            var cm = new System.Windows.Controls.ContextMenu();
            var miOpen = new System.Windows.Controls.MenuItem { Header = "Open in HearthstoneJSON" };
            miOpen.Click += ContextOpenBrowser_Click;
            cm.Items.Add(miOpen);
            cm.Items.Add(new System.Windows.Controls.Separator());
            var miReg = new System.Windows.Controls.MenuItem { Header = "Never suggest (Regular)" };
            miReg.Click += ContextNeverRegular_Click;
            cm.Items.Add(miReg);
            var miGold = new System.Windows.Controls.MenuItem { Header = "Never suggest (Golden)" };
            miGold.Click += ContextNeverGolden_Click;
            cm.Items.Add(miGold);
            var miAny = new System.Windows.Controls.MenuItem { Header = "Never suggest (any premium)" };
            miAny.Click += ContextNeverAny_Click;
            cm.Items.Add(miAny);
            // PlacementTarget must be the row itself so GetContextRow can navigate back.
            cm.PlacementTarget = dgRow;
            dgRow.ContextMenu = cm;
        }

        private DustItemRow GetContextRow(object sender)
        {
            // sender is a MenuItem; navigate up to DataGridRow via ContextMenu.PlacementTarget.
            if (sender is System.Windows.Controls.MenuItem mi
                && mi.Parent is System.Windows.Controls.ContextMenu cm
                && cm.PlacementTarget is System.Windows.FrameworkElement fe
                && fe.DataContext is DustItemRow row)
                return row;
            return null;
        }

        private void ContextOpenBrowser_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var row = GetContextRow(sender);
            if (row == null) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"https://hearthstonejson.com/cards/{row.CardId}",
                    UseShellExecute = true,
                });
            }
            catch { /* swallow; best-effort */ }
        }

        private void AddNeverSuggest(string cardId, params DustAdvisor.Algorithm.Domain.Premium[] tiers)
        {
            var existing = new System.Collections.Generic.HashSet<(string, DustAdvisor.Algorithm.Domain.Premium)>(
                _neverRepo.Load(_neverSuggestPath));
            foreach (var t in tiers) existing.Add((cardId, t));
            _neverRepo.Save(_neverSuggestPath, existing);
            // Remove the row from the visible list (next recompute will exclude it too).
            if (ItemsGrid.ItemsSource is System.Collections.Generic.IEnumerable<DustItemRow> rows)
                ItemsGrid.ItemsSource = rows.Where(r => r.CardId != cardId).ToList();
            _toasts.Show($"Never suggest: {cardId}", onClick: () =>
            {
                var rollback = new System.Collections.Generic.HashSet<(string, DustAdvisor.Algorithm.Domain.Premium)>(_neverRepo.Load(_neverSuggestPath));
                foreach (var t in tiers) rollback.Remove((cardId, t));
                _neverRepo.Save(_neverSuggestPath, rollback);
                _toasts.Show($"Restored: {cardId}");
            });
            Render(_plan);
        }

        private void ContextNeverRegular_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var row = GetContextRow(sender);
            if (row != null) AddNeverSuggest(row.CardId, DustAdvisor.Algorithm.Domain.Premium.Regular);
        }

        private void ContextNeverGolden_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var row = GetContextRow(sender);
            if (row != null) AddNeverSuggest(row.CardId, DustAdvisor.Algorithm.Domain.Premium.Golden);
        }

        private void ContextNeverAny_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var row = GetContextRow(sender);
            if (row != null) AddNeverSuggest(row.CardId,
                DustAdvisor.Algorithm.Domain.Premium.Regular,
                DustAdvisor.Algorithm.Domain.Premium.Golden,
                DustAdvisor.Algorithm.Domain.Premium.Signature);
        }

        private void DustAdvisorWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Don't intercept keys when typing in any text input (target dust, search, etc.).
            if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;

            bool ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
            bool shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;

            if (ctrl && e.Key == System.Windows.Input.Key.Z && !shift)
            {
                var label = _undo.Undo();
                if (label != null) _toasts.Show($"Undid: {label}");
                e.Handled = true;
                return;
            }
            if (ctrl && (e.Key == System.Windows.Input.Key.Y || (shift && e.Key == System.Windows.Input.Key.Z)))
            {
                var label = _undo.Redo();
                if (label != null) _toasts.Show($"Redid: {label}");
                e.Handled = true;
                return;
            }

            var row = ItemsGrid.SelectedItem as DustItemRow;
            if (row == null) return;

            switch (e.Key)
            {
                case System.Windows.Input.Key.D:
                    row.IsSelected = true;
                    if (AutoAdvanceBox.IsChecked == true)
                        ItemsGrid.SelectedIndex = System.Math.Min(ItemsGrid.SelectedIndex + 1, ItemsGrid.Items.Count - 1);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.K:
                    AddNeverSuggest(row.CardId,
                        DustAdvisor.Algorithm.Domain.Premium.Regular,
                        DustAdvisor.Algorithm.Domain.Premium.Golden,
                        DustAdvisor.Algorithm.Domain.Premium.Signature);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Space:
                    row.IsSelected = !row.IsSelected;
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Enter:
                    var item = _plan.Items.FirstOrDefault(x => x.CardId == row.CardId);
                    if (item != null)
                    {
                        var win = new CardDetailWindow(item, _artCache) { Owner = this };
                        win.Show();
                    }
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Escape:
                    ClearCart();
                    e.Handled = true;
                    break;
            }
        }
    }
}
