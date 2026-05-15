using System;
using System.IO;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class MetaTierRepositoryTests
    {
        [Fact]
        public void Load_returns_map_from_cardId_to_tier()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "meta_tiers.json");
            var repo = new MetaTierRepository();
            var map = repo.Load(path);

            map["EX1_001"].Should().Be("S");
            map["EX1_002"].Should().Be("A");
            map.Should().HaveCount(2);
        }

        [Fact]
        public void Load_returns_empty_when_file_missing()
        {
            var repo = new MetaTierRepository();
            var map = repo.Load(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N") + ".json"));
            map.Should().BeEmpty();
        }
    }
}
