using System;
using System.IO;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Data;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Data.Tests
{
    public class NeverSuggestRepositoryTests
    {
        [Fact]
        public void Load_returns_keyed_tuples_for_all_entries()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "never_suggest.json");
            var repo = new NeverSuggestRepository();
            var set = repo.Load(path);

            set.Should().Contain(("EX1_565", Premium.Regular));
            set.Should().Contain(("CORE_AT_001", Premium.Golden));
            set.Should().HaveCount(2);
        }

        [Fact]
        public void Load_returns_empty_when_file_missing()
        {
            var repo = new NeverSuggestRepository();
            var set = repo.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".json"));
            set.Should().BeEmpty();
        }
    }
}
