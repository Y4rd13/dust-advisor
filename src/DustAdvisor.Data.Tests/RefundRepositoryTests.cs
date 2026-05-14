using System;
using System.IO;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class RefundRepositoryTests
    {
        [Fact]
        public void Load_returns_only_unexpired_card_ids_relative_to_now()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "refund.json");
            var repo = new RefundRepository();
            var set = repo.Load(path, now: new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero));

            set.Should().Contain("NERFED_001");
            set.Should().NotContain("EXPIRED_001");
        }
    }
}
