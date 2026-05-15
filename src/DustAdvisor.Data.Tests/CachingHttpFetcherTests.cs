using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class CachingHttpFetcherTests
    {
        [Fact]
        public async Task GetAsync_writes_response_to_cache_and_reads_from_cache_when_inner_throws()
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "DustAdvisorCacheTest-" + Guid.NewGuid().ToString("N"));
            try
            {
                var stub = new StubInner();
                var sut = new CachingHttpFetcher(stub, cacheDir);

                stub.NextResponse = "FIRST";
                var first = await sut.GetAsync("https://x/cards.json", CancellationToken.None);
                first.Should().Be("FIRST");

                stub.ShouldThrow = true;
                var second = await sut.GetAsync("https://x/cards.json", CancellationToken.None);
                second.Should().Be("FIRST"); // returned from disk
            }
            finally
            {
                if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
            }
        }

        [Fact]
        public async Task GetAsync_returns_cached_bytes_without_calling_inner_when_cache_is_fresh()
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "FreshCache-" + Guid.NewGuid().ToString("N"));
            try
            {
                var stub = new StubInner { NextResponse = "FRESH" };
                var sut = new CachingHttpFetcher(stub, cacheDir);

                // First call populates the cache.
                await sut.GetAsync("https://x/cards.json", CancellationToken.None);

                // Subsequent call: stub would throw, but fresh cache must short-circuit.
                stub.ShouldThrow = true;
                var second = await sut.GetAsync("https://x/cards.json", CancellationToken.None);

                second.Should().Be("FRESH");
            }
            finally
            {
                if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
            }
        }

        [Fact]
        public async Task GetAsync_refetches_when_cache_is_stale()
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "StaleCache-" + Guid.NewGuid().ToString("N"));
            try
            {
                var stub = new StubInner { NextResponse = "FIRST" };
                var sut = new CachingHttpFetcher(stub, cacheDir);
                await sut.GetAsync("https://x/cards.json", CancellationToken.None);

                // Backdate the cache file to make it look stale (10 days old).
                var cachePath = Directory.GetFiles(cacheDir, "*.json")[0];
                File.SetLastWriteTimeUtc(cachePath, DateTime.UtcNow - TimeSpan.FromDays(10));

                stub.NextResponse = "REFRESHED";
                var second = await sut.GetAsync("https://x/cards.json", CancellationToken.None);

                second.Should().Be("REFRESHED");
            }
            finally
            {
                if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, recursive: true);
            }
        }

        private sealed class StubInner : IHttpFetcher
        {
            public string NextResponse { get; set; }
            public bool ShouldThrow { get; set; }
            public Task<string> GetAsync(string url, CancellationToken ct)
            {
                if (ShouldThrow) throw new System.Net.Http.HttpRequestException("offline");
                return Task.FromResult(NextResponse);
            }
        }
    }
}
