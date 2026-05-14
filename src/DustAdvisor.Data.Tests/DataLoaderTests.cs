using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class DataLoaderTests
    {
        [Fact]
        public async Task LoadAsync_composes_meta_uncraftable_refund_and_never_suggest()
        {
            var cardsPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_cards.json");
            var unPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "uncraftable.json");
            var refundPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "refund.json");
            var neverPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "never_suggest.json");

            var loader = new DataLoader(
                new HearthstoneJsonClient(new HearthstoneJsonClientTests.LocalFileFetcherPublic(cardsPath)),
                new UncraftableRepository(),
                new RefundRepository(),
                new NeverSuggestRepository());

            var snapshot = await loader.LoadAsync(
                locale: "enUS",
                uncraftablePath: unPath,
                refundPath: refundPath,
                neverSuggestPath: neverPath,
                now: new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero),
                ct: CancellationToken.None);

            snapshot.Meta.Should().HaveCount(3);
            // Uncraftable now merges: 2 curated + 2 never-suggest = 4 total (assuming no overlap)
            snapshot.Uncraftable.Should().HaveCount(4);
            snapshot.RefundWindow.Should().ContainSingle().Which.Should().Be("NERFED_001");
        }
    }
}
