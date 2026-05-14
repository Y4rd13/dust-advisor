using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Ui.Export;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Ui.Tests
{
    public class CardArtCacheTests
    {
        [Fact]
        public async Task GetAsync_downloads_and_caches_on_first_hit()
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "art_cache_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var stub = new StubFetcher();
                stub.NextBytes = new byte[] { 1, 2, 3, 4 };
                var cache = new CardArtCache(stub, cacheDir);

                var first = await cache.GetAsync("EX1_001", size: 256, ct: CancellationToken.None);
                first.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });
                stub.CallCount.Should().Be(1);

                stub.NextBytes = new byte[] { 9, 9, 9, 9 };
                var second = await cache.GetAsync("EX1_001", size: 256, ct: CancellationToken.None);
                second.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });
                stub.CallCount.Should().Be(1); // served from disk cache
            }
            finally
            {
                if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
            }
        }

        [Fact]
        public async Task GetAsync_returns_null_when_fetcher_fails_and_no_cache()
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "art_cache_fail_" + Guid.NewGuid().ToString("N"));
            try
            {
                var stub = new StubFetcher { ShouldThrow = true };
                var cache = new CardArtCache(stub, cacheDir);

                var result = await cache.GetAsync("EX1_999", size: 256, ct: CancellationToken.None);
                result.Should().BeNull();
            }
            finally
            {
                if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
            }
        }

        private sealed class StubFetcher : IBinaryFetcher
        {
            public byte[] NextBytes { get; set; } = new byte[0];
            public bool ShouldThrow { get; set; }
            public int CallCount { get; private set; }
            public Task<byte[]> GetBytesAsync(string url, CancellationToken ct)
            {
                CallCount++;
                if (ShouldThrow) throw new System.Net.Http.HttpRequestException("no network");
                return Task.FromResult(NextBytes);
            }
        }
    }
}
