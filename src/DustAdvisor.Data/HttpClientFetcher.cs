using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    public sealed class HttpClientFetcher : IHttpFetcher
    {
        private readonly HttpClient _http;
        public HttpClientFetcher(HttpClient http) { _http = http; }
        public async Task<string> GetAsync(string url, CancellationToken ct)
        {
            using (var resp = await _http.GetAsync(url, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }
}
