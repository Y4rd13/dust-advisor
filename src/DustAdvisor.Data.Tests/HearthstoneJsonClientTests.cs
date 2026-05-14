using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class HearthstoneJsonClientTests
    {
        [Fact]
        public async Task LoadCollectibleAsync_parses_fixture_and_excludes_non_collectible()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_cards.json");
            var client = new HearthstoneJsonClient(new LocalFileFetcherPublic(path));

            var cards = await client.LoadCollectibleAsync(locale: "enUS", ct: CancellationToken.None);

            cards.Should().HaveCount(3); // non-collectible "NON_COLL" excluded
            cards.Should().Contain(c => c.CardId == "EX1_001" && c.Set.IsStandardLegal == false);
            cards.Should().Contain(c => c.CardId == "STD_001" && c.Set.IsStandardLegal == true);
            cards.Should().Contain(c => c.CardId == "CORE_001" && c.Set.IsCore);
        }

        internal sealed class LocalFileFetcherPublic : IHttpFetcher
        {
            private readonly string _path;
            public LocalFileFetcherPublic(string path) { _path = path; }
            public Task<string> GetAsync(string url, CancellationToken ct)
                => Task.FromResult(File.ReadAllText(_path));
        }
    }
}
