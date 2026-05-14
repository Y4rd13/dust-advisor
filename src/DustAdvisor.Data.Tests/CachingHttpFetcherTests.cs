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
