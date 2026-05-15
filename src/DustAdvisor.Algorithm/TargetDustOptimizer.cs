using System.Collections.Generic;
using System.Linq;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm
{
    public sealed class TargetDustOptimizerResult
    {
        public IReadOnlyList<DustItem> Items { get; }
        public int AchievedDust { get; }
        public int Target { get; }
        public bool TargetMet => AchievedDust >= Target && Target > 0;

        public TargetDustOptimizerResult(IReadOnlyList<DustItem> items, int achievedDust, int target)
        {
            Items = items;
            AchievedDust = achievedDust;
            Target = target;
        }
    }

    public static class TargetDustOptimizer
    {
        public static TargetDustOptimizerResult Optimize(IEnumerable<DustItem> items, int target)
        {
            if (target <= 0)
                return new TargetDustOptimizerResult(new List<DustItem>(), 0, target);

            // Sort by IsStandardLegal asc (false before true → Wild first), then DustGained desc (biggest first).
            var sorted = items
                .OrderBy(i => i.IsStandardLegal)
                .ThenByDescending(i => i.DustGained)
                .ToList();

            var picked = new List<DustItem>();
            int total = 0;
            foreach (var i in sorted)
            {
                if (total >= target) break;
                picked.Add(i);
                total += i.DustGained;
            }
            return new TargetDustOptimizerResult(picked, total, target);
        }
    }
}
