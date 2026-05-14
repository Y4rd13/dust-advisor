using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Ui.Export
{
    public interface IBinaryFetcher
    {
        Task<byte[]> GetBytesAsync(string url, CancellationToken ct);
    }

    public sealed class CardArtCache
    {
        private const string BaseUrl = "https://art.hearthstonejson.com/v1/render/latest";

        private readonly IBinaryFetcher _fetcher;
        private readonly string _cacheDir;
        private readonly string _locale;

        public CardArtCache(IBinaryFetcher fetcher, string cacheDir, string locale = "enUS")
        {
            _fetcher = fetcher;
            _cacheDir = cacheDir;
            _locale = string.IsNullOrEmpty(locale) ? "enUS" : locale;
            Directory.CreateDirectory(_cacheDir);
        }

        public async Task<byte[]> GetAsync(string cardId, int size, CancellationToken ct)
        {
            var path = Path.Combine(_cacheDir, $"{cardId}_{size}.png");
            if (File.Exists(path)) return File.ReadAllBytes(path);

            var url = $"{BaseUrl}/{_locale}/{size}x/{cardId}.png";
            try
            {
                var bytes = await _fetcher.GetBytesAsync(url, ct).ConfigureAwait(false);
                File.WriteAllBytes(path, bytes);
                return bytes;
            }
            catch
            {
                return null;
            }
        }
    }
}
