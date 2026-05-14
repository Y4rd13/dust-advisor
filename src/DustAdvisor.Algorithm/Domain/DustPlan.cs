using System.Collections.Generic;

namespace DustAdvisor.Algorithm.Domain
{
    public sealed class DustPlan
    {
        public IReadOnlyList<DustItem> Items { get; }
        public IReadOnlyList<Warning> Warnings { get; }
        public int TotalDust { get; }

        public DustPlan(IReadOnlyList<DustItem> items, IReadOnlyList<Warning> warnings, int totalDust)
        {
            Items = items;
            Warnings = warnings;
            TotalDust = totalDust;
        }
    }
}
