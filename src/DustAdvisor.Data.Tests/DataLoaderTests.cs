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
        public async Task LoadAsync_composes_meta_uncraftable_and_refund()
        {
            var cardsPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample_cards.json");
            var unPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "uncraftable.json");
            var refundPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "refund.json");

            var loader = new DataLoader(
                new HearthstoneJsonClient(new HearthstoneJsonClientTests.LocalFileFetcherPublic(cardsPath)),
                new UncraftableRepository(),
                new RefundRepository());

            var snapshot = await loader.LoadAsync(
                locale: "enUS",
                uncraftablePath: unPath,
                refundPath: refundPath,
                now: new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero),
                ct: CancellationToken.None);

            snapshot.Meta.Should().HaveCount(3);
            snapshot.Uncraftable.Should().HaveCount(2);
            snapshot.RefundWindow.Should().ContainSingle().Which.Should().Be("NERFED_001");
        }
    }
}
