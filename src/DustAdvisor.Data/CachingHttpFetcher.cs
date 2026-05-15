using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    public sealed class CachingHttpFetcher : IHttpFetcher
    {
        public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

        private readonly IHttpFetcher _inner;
        private readonly string _cacheDir;

        public CachingHttpFetcher(IHttpFetcher inner, string cacheDir)
        {
            _inner = inner;
            _cacheDir = cacheDir;
            Directory.CreateDirectory(_cacheDir);
        }

        public async Task<string> GetAsync(string url, CancellationToken ct)
        {
            var path = CachePath(url);

            // Fresh cache: serve from disk without calling the network.
            if (File.Exists(path))
            {
                var ageOk = (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)) <= MaxAge;
                if (ageOk) return File.ReadAllText(path, Encoding.UTF8);
            }

            try
            {
                var fresh = await _inner.GetAsync(url, ct).ConfigureAwait(false);
                File.WriteAllText(path, fresh, Encoding.UTF8);
                return fresh;
            }
            catch
            {
                if (File.Exists(path)) return File.ReadAllText(path, Encoding.UTF8);
                throw;
            }
        }

        private string CachePath(string url)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                var name = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant() + ".json";
                return Path.Combine(_cacheDir, name);
            }
        }
    }
}
