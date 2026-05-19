using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    public sealed class HttpClientPatchFetcher : IPatchHttpFetcher
    {
        private readonly HttpClient _http;

        public HttpClientPatchFetcher(HttpClient http) { _http = http; }

        public async Task<string> HeadLastModifiedAsync(string url, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Head, url))
            using (var resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                return resp.Content.Headers.LastModified?.ToString("r");
            }
        }

        public async Task<(string Body, string LastModified)> GetWithLastModifiedAsync(string url, CancellationToken ct)
        {
            using (var resp = await _http.GetAsync(url, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var lm = resp.Content.Headers.LastModified?.ToString("r");
                return (body, lm);
            }
        }
    }
}
