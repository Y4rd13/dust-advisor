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
            Why = BuildWhy(item);
            Class = item.Class;
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
