using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Ui.Export;

namespace DustAdvisor.Ui
{
    public sealed class DustItemRow : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public string CardId { get; }
        public string Name { get; }
        public string Rarity { get; }
        public int RegularToDust { get; }
        public int GoldenToDust { get; }
        public int DustGained { get; }
        public int InDeckCount { get; }
        public string Flag { get; }
        public string Why { get; }
        public string Class { get; }
        public string MetaTier { get; }
        public string WinRateDisplay { get; }
        public string OwnedSummary { get; }
        public string DustCalculation { get; }

        private BitmapImage _art;
        public BitmapImage Art
        {
            get { return _art; }
            private set { _art = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Art))); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public DustItemRow(DustItem item)
        {
            CardId = item.CardId;
            Name = item.CardName;
            Rarity = item.Rarity.ToString();
            RegularToDust = item.RegularToDust;
            GoldenToDust = item.GoldenToDust;
            DustGained = item.DustGained;
            InDeckCount = item.InDeckCount;
            // Flag precedence: IN-DECK > REFUND > STANDARD > WILD
            Flag = item.InDeckCount > 0 ? "IN-DECK"
                 : item.InRefundWindow ? "REFUND"
                 : item.IsStandardLegal ? "STANDARD"
                 : "WILD";
            OwnedSummary = BuildOwnedSummary(item);
            DustCalculation = BuildDustCalculation(item);
            Why = BuildWhy(item) + "\n\n" + DustCalculation;
            Class = item.Class;
            MetaTier = item.MetaTier;
            WinRateDisplay = item.WinRate.HasValue ? $"{item.WinRate.Value * 100:0}%" : "";
        }

        private static string BuildOwnedSummary(DustItem item)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (item.OwnedRegular > 0) parts.Add($"{item.OwnedRegular}R");
            if (item.OwnedGolden > 0) parts.Add($"{item.OwnedGolden}G");
            if (item.OwnedDiamond > 0) parts.Add($"{item.OwnedDiamond}D");
            if (item.OwnedSignature > 0) parts.Add($"{item.OwnedSignature}S");
            return string.Join("+", parts);
        }

        private static string BuildDustCalculation(DustItem item)
        {
            int playset = item.Rarity == DustAdvisor.Algorithm.Domain.Rarity.Legendary ? 1 : 2;
            var lines = new System.Collections.Generic.List<string>();
            lines.Add($"Owned: {item.OwnedRegular}R + {item.OwnedGolden}G + {item.OwnedDiamond}D + {item.OwnedSignature}S (playset {playset})");
            int cosmeticHeld = item.OwnedDiamond + item.OwnedSignature;
            if (cosmeticHeld > 0)
                lines.Add($"Cosmetic copies (Diamond+Signature) cover {System.Math.Min(cosmeticHeld, playset)} of {playset} playset slots.");
            var parts = new System.Collections.Generic.List<string>();
            if (item.RegularToDust > 0) parts.Add($"{item.RegularToDust} regular × {item.UnitRegular} = {item.RegularToDust * item.UnitRegular}");
            if (item.GoldenToDust > 0) parts.Add($"{item.GoldenToDust} golden × {item.UnitGolden} = {item.GoldenToDust * item.UnitGolden}");
            if (parts.Count == 0)
                lines.Add($"No copies marked for dust → 0 dust.");
            else
                lines.Add($"Disenchant: {string.Join(" + ", parts)} = {item.DustGained} dust");
            if (item.InRefundWindow)
                lines.Add("(REFUND window: full craft cost recoverable; values above use craft cost, not standard disenchant.)");
            return string.Join("\n", lines);
        }

        public async Task EnsureArtLoadedAsync(CardArtCache cache, int size = 256)
        {
            if (Art != null) return;
            var bytes = await cache.GetAsync(CardId, size, System.Threading.CancellationToken.None).ConfigureAwait(false);
            if (bytes == null) return;

            BitmapImage bmp = null;
            await Task.Run(() =>
            {
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
            });
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Art = bmp);
        }

        private static string BuildWhy(DustItem item)
        {
            if (item.InDeckCount > 0)
                return $"Used in {item.InDeckCount} of your decks. Switch off SafeOnlyUnused if you really want to dust this.";
            if (item.InRefundWindow)
                return "In refund window: full craft cost recoverable. Highest dust-per-card priority.";
            if (item.IsStandardLegal)
                return "Standard-legal: may still be useful in current meta. SafeOnly skips by default.";
            return "Wild-only: rotated out of Standard. Low opportunity cost to dust.";
        }
    }
}
