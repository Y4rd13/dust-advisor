using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Ui.Export;

namespace DustAdvisor.Ui
{
    public sealed class HttpBinaryFetcher : IBinaryFetcher
    {
        private static readonly HttpClient SharedClient = new HttpClient();

        public async Task<byte[]> GetBytesAsync(string url, CancellationToken ct)
        {
            using (var resp = await SharedClient.GetAsync(url, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }
    }
}
