using System;
using System.IO;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class UncraftableRepositoryTests
    {
        [Fact]
        public void Load_returns_keyed_tuples_for_all_entries()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "uncraftable.json");
            var repo = new UncraftableRepository();
            var set = repo.Load(path);

            set.Should().Contain(("REWARD_001", Premium.Golden));
            set.Should().Contain(("TAVERN_001", Premium.Signature));
            set.Should().HaveCount(2);
        }
    }
}
