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

        [Fact]
        public void Save_then_Load_roundtrips()
        {
            var path = Path.Combine(Path.GetTempPath(), "never_suggest_test_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var repo = new NeverSuggestRepository();
                var entries = new[]
                {
                    ("EX1_001", Premium.Regular),
                    ("EX1_002", Premium.Golden),
                };
                repo.Save(path, entries);

                var loaded = repo.Load(path);
                loaded.Should().Contain(("EX1_001", Premium.Regular));
                loaded.Should().Contain(("EX1_002", Premium.Golden));
                loaded.Should().HaveCount(2);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Save_writes_atomically_via_temp_file()
        {
            var path = Path.Combine(Path.GetTempPath(), "never_suggest_atomic_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, "[{\"cardId\":\"OLD\",\"premium\":\"Regular\"}]");
                var repo = new NeverSuggestRepository();
                repo.Save(path, new[] { ("NEW", Premium.Golden) });

                var loaded = repo.Load(path);
                loaded.Should().ContainSingle().Which.Should().Be(("NEW", Premium.Golden));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
