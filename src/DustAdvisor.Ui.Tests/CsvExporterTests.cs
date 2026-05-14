using System.Collections.Generic;
using DustAdvisor.Algorithm.Domain;
using DustAdvisor.Ui.Export;
using FluentAssertions;
using Xunit;

namespace DustAdvisor.Ui.Tests
{
    public class CsvExporterTests
    {
        [Fact]
        public void ToCsv_emits_header_and_one_row_per_item()
        {
            var plan = new DustPlan(
                items: new List<DustItem>
                {
                    new DustItem("A", "Alpha", Rarity.Common, regularToDust: 3, goldenToDust: 0,
                                 dustGained: 15, inRefundWindow: false, isStandardLegal: false)
                },
                warnings: new List<Warning>(),
                totalDust: 15);

            var csv = CsvExporter.ToCsv(plan);
            csv.Should().StartWith("CardId,Name,Rarity,RegularToDust,GoldenToDust,DustGained,InRefundWindow,IsStandardLegal");
            csv.Should().Contain("A,Alpha,Common,3,0,15,False,False");
        }
    }
}
