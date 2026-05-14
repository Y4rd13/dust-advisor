using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Ui.Export;

namespace DustAdvisor.Ui
{
    public partial class CardDetailWindow : Window
    {
        public CardDetailWindow(DustItem item, CardArtCache cache)
        {
            InitializeComponent();
            Title = item.CardName;
            HeaderText.Text = $"{item.CardName} — {item.Rarity}";

            var flag = item.InRefundWindow ? "REFUND" : item.IsStandardLegal ? "STANDARD-LEGAL" : "WILD";
            DetailsText.Text =
                $"Regulars to dust: {item.RegularToDust}\n" +
                $"Goldens to dust:  {item.GoldenToDust}\n" +
                $"Dust gained:      {item.DustGained:n0}\n" +
                $"Format:           {flag}\n" +
                $"Card id:          {item.CardId}";

            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            Loaded += async (s, e) =>
            {
                var bytes = await cache.GetAsync(item.CardId, size: 512, ct: CancellationToken.None);
                if (bytes == null) return;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
                ArtImage.Source = bmp;
            };
        }
    }
}
