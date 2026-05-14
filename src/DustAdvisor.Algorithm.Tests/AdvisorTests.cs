using System.Collections.Generic;
using DustAdvisor.Algorithm;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Algorithm.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Algorithm.Tests
{
    public class AdvisorTests
    {
        [Fact]
        public void Excess_regulars_above_playset_are_safe_to_dust()
        {
            var meta = CardFixtures.CommonWild("EX1_001", 1);
            var inputs = new AdvisorInputs(
                collection: new[] { new CollectionEntry("EX1_001", regular: 5) },
                meta: new[] { meta },
                uncraftable: new HashSet<(string, Premium)>(),
                refundWindow: new HashSet<string>(),
                options: new AdvisorOptions());

            var plan = new Advisor().Recommend(inputs);

            plan.Items.Should().HaveCount(1);
            plan.Items[0].RegularToDust.Should().Be(3);
            plan.Items[0].GoldenToDust.Should().Be(0);
            plan.Items[0].DustGained.Should().Be(15);
            plan.TotalDust.Should().Be(15);
        }
    }
}
