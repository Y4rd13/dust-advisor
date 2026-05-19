using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Data;
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
        private readonly IReadOnlyDictionary<string, RefundEntry> _refundDetails;

        public DustAdvisorWindow(DustPlan plan, Func<AdvisorOptions, DustPlan> recompute, CardArtCache artCache, string neverSuggestPath,
            IReadOnlyDictionary<string, RefundEntry> refundDetails = null)
        {
            InitializeComponent();
            _plan = plan;
            _recompute = recompute;
            _artCache = artCache;
            _neverSuggestPath = neverSuggestPath;
            _refundDetails = refundDetails ?? new Dictionary<string, RefundEntry>();
            _toasts = new ToastHost(ToastContainer);
            StrategyBox.ItemsSource = new[] { Strategy.SafeOnly, Strategy.SafeOnlyUnused, Strategy.RotationImminent, Strategy.MaxDust, Strategy.RefundOnly };
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
            foreach (var box in new[] { RarityCommonBox, RarityRareBox, RarityEpicBox, RarityLegendaryBox, ShowNormalBox, ShowGoldenBox, HideBuffsBox })
            {
                box.Checked += refilter;
                box.Unchecked += refilter;
            }

            foreach (var box in new[] {
                ClassDruidBox, ClassHunterBox, ClassMageBox, ClassPaladinBox, ClassPriestBox,
                ClassRogueBox, ClassShamanBox, ClassWarlockBox, ClassWarriorBox,
                ClassDhBox, ClassDkBox, ClassNeutralBox })
            {
                box.Checked += refilter;
                box.Unchecked += refilter;
            }

            TargetDustBox.TextChanged += (s, e) => Render(_plan);
            SearchBox.TextChanged += (s, e) => Render(_plan);

            ExportCsvButton.Click += (s, e) => SaveAs("CSV (*.csv)|*.csv", DustAdvisor.Ui.Export.CsvExporter.ToCsv(BuildFilteredPlan()));
            ExportJsonButton.Click += (s, e) => SaveAs("JSON (*.json)|*.json", DustAdvisor.Ui.Export.JsonExporter.ToJson(BuildFilteredPlan()));

            ConfirmButton.Click += (s, e) => ConfirmCart();
            ClearCartButton.Click += (s, e) => ClearCart();

            this.PreviewKeyDown += DustAdvisorWindow_PreviewKeyDown;
        }

        private void HelpButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var help = new HelpWindow { Owner = this };
            help.ShowDialog();
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
                    ? Localization.Format("DA_TargetStatus_Met_Fmt",
                        optimized.AchievedDust.ToString("n0"), t.ToString("n0"), visible.Count)
                    : Localization.Format("DA_TargetStatus_NotMet_Fmt",
                        optimized.AchievedDust.ToString("n0"), t.ToString("n0"));
                TargetStatusLabel.Foreground =
                    (System.Windows.Media.Brush)TryFindResource(optimized.TargetMet ? "SuccessBrush" : "WarningBrush")
                    ?? (optimized.TargetMet ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.OrangeRed);
            }
            else
            {
                visible = filtered;
                TargetStatusLabel.Text = string.Empty;
            }

            var visibleDust = visible.Sum(i => i.DustGained);
            TotalDustLabel.Text = (target.HasValue || visible.Count != plan.Items.Count)
                ? Localization.Format("DA_TotalDust_Filtered_Fmt",
                    visibleDust.ToString("n0"), plan.TotalDust.ToString("n0"))
                : Localization.Format("DA_TotalDust_Fmt", plan.TotalDust.ToString("n0"));
            WarningCountLabel.Text = plan.Warnings.Count > 0
                ? Localization.Format("DA_Warning_Count_Fmt", plan.Warnings.Count)
                : string.Empty;
            ItemsGrid.ItemsSource = visible.Select(i =>
            {
                _refundDetails.TryGetValue(i.CardId, out var refundEntry);
                return new DustItemRow(i, refundEntry);
            }).ToList();
            foreach (var row in (System.Collections.Generic.IEnumerable<DustItemRow>)ItemsGrid.ItemsSource)
            {
                row.PropertyChanged += Row_PropertyChanged;
            }
            UpdateConfirmBar();
            StatusFooter.Text = Localization.Format("DA_StatusFooter_Fmt",
                _ledger.Entries.Count, _ledger.TotalDust.ToString("n0"), visible.Count, plan.Items.Count);
            UpdateWastedDustHint();
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
            string searchText = SearchBox.Text ?? "";

            var classes = new HashSet<string>();
            if (ClassDruidBox.IsChecked == true) classes.Add("DRUID");
            if (ClassHunterBox.IsChecked == true) classes.Add("HUNTER");
            if (ClassMageBox.IsChecked == true) classes.Add("MAGE");
            if (ClassPaladinBox.IsChecked == true) classes.Add("PALADIN");
            if (ClassPriestBox.IsChecked == true) classes.Add("PRIEST");
            if (ClassRogueBox.IsChecked == true) classes.Add("ROGUE");
            if (ClassShamanBox.IsChecked == true) classes.Add("SHAMAN");
            if (ClassWarlockBox.IsChecked == true) classes.Add("WARLOCK");
            if (ClassWarriorBox.IsChecked == true) classes.Add("WARRIOR");
            if (ClassDhBox.IsChecked == true) classes.Add("DEMONHUNTER");
            if (ClassDkBox.IsChecked == true) classes.Add("DEATHKNIGHT");
            if (ClassNeutralBox.IsChecked == true) classes.Add("NEUTRAL");

            bool hideBuffs = HideBuffsBox.IsChecked == true;

            foreach (var i in items)
            {
                if (!rarities.Contains(i.Rarity)) continue;
                if (format == FormatFilter.Standard && !i.IsStandardLegal) continue;
                if (format == FormatFilter.Wild && i.IsStandardLegal) continue;
                if (!showNormal && i.RegularToDust > 0 && i.GoldenToDust == 0) continue;
                if (!showGolden && i.GoldenToDust > 0 && i.RegularToDust == 0) continue;
                if (!showNormal && !showGolden) continue;
                if (searchText.Length > 0 && i.CardName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!classes.Contains(i.Class ?? "NEUTRAL")) continue;
                if (hideBuffs && _refundDetails.TryGetValue(i.CardId, out var entry)
                    && entry.AggregateDirection == PatchChangeDirection.Buff) continue;
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
                    ConfirmText.Text = Localization.Format("DA_Confirm_Selected_Fmt", selected, dust.ToString("n0"));
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
                label: Localization.Format("DA_Undo_Marked_Fmt", cardIds.Count),
                undo: () => { _ledger.Undo(); foreach (var r in selected) r.IsSelected = true; });
            foreach (var r in selected) r.IsSelected = false;
            _toasts.Show(
                Localization.Format("DA_Toast_Marked_Fmt", cardIds.Count, dust.ToString("n0")),
                onClick: () => { _undo.Undo(); });
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
            var miOpen = new System.Windows.Controls.MenuItem { Header = Localization.Get("DA_Menu_OpenInBrowser") };
            miOpen.Click += ContextOpenBrowser_Click;
            cm.Items.Add(miOpen);
            cm.Items.Add(new System.Windows.Controls.Separator());
            var miReg = new System.Windows.Controls.MenuItem { Header = Localization.Get("DA_Menu_NeverRegular") };
            miReg.Click += ContextNeverRegular_Click;
            cm.Items.Add(miReg);
            var miGold = new System.Windows.Controls.MenuItem { Header = Localization.Get("DA_Menu_NeverGolden") };
            miGold.Click += ContextNeverGolden_Click;
            cm.Items.Add(miGold);
            var miAny = new System.Windows.Controls.MenuItem { Header = Localization.Get("DA_Menu_NeverAny") };
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
            _toasts.Show(Localization.Format("DA_Toast_NeverSuggest_Fmt", cardId), onClick: () =>
            {
                var rollback = new System.Collections.Generic.HashSet<(string, DustAdvisor.Algorithm.Domain.Premium)>(_neverRepo.Load(_neverSuggestPath));
                foreach (var t in tiers) rollback.Remove((cardId, t));
                _neverRepo.Save(_neverSuggestPath, rollback);
                _toasts.Show(Localization.Format("DA_Toast_Restored_Fmt", cardId));
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

        private void UpdateWastedDustHint()
        {
            // Only show the hint when current strategy isn't already MaxDust.
            var current = (Strategy)StrategyBox.SelectedItem;
            if (current == Strategy.MaxDust)
            {
                WastedDustLabel.Text = string.Empty;
                return;
            }

            var maxOpts = new AdvisorOptions(
                strategy: Strategy.MaxDust,
                keepStandardLegal: KeepStandardBox.IsChecked == true);
            var maxPlan = _recompute(maxOpts);
            int delta = maxPlan.TotalDust - _plan.TotalDust;
            WastedDustLabel.Text = delta > 0
                ? Localization.Format("DA_WastedDust_Fmt", delta.ToString("n0"))
                : string.Empty;
        }

        private void Row_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DustItemRow.IsSelected))
                UpdateConfirmBar();
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
                if (label != null) _toasts.Show(Localization.Format("DA_Toast_Undid_Fmt", label));
                e.Handled = true;
                return;
            }
            if (ctrl && (e.Key == System.Windows.Input.Key.Y || (shift && e.Key == System.Windows.Input.Key.Z)))
            {
                var label = _undo.Redo();
                if (label != null) _toasts.Show(Localization.Format("DA_Toast_Redid_Fmt", label));
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
