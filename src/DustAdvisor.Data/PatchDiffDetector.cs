using System;
using System.Collections.Generic;

namespace DustAdvisor.Data
{
    public static class PatchDiffDetector
    {
        public static IReadOnlyList<PatchChange> Diff(
            IEnumerable<CardSnapshot> previous,
            IEnumerable<CardSnapshot> current)
        {
            var prevById = new Dictionary<string, CardSnapshot>();
            foreach (var p in previous)
            {
                if (p?.Id != null) prevById[p.Id] = p;
            }

            var result = new List<PatchChange>();
            foreach (var curr in current)
            {
                if (curr?.Id == null) continue;
                if (!prevById.TryGetValue(curr.Id, out var prev)) continue;

                AddStatChange(result, curr, "cost",       prev.Cost,       curr.Cost,       CostDirection);
                AddStatChange(result, curr, "attack",     prev.Attack,     curr.Attack,     StatDirection);
                AddStatChange(result, curr, "health",     prev.Health,     curr.Health,     StatDirection);
                AddStatChange(result, curr, "durability", prev.Durability, curr.Durability, StatDirection);
                AddStatChange(result, curr, "armor",      prev.Armor,      curr.Armor,      StatDirection);

                if (!string.Equals(prev.Text ?? string.Empty, curr.Text ?? string.Empty, StringComparison.Ordinal))
                {
                    result.Add(new PatchChange(
                        curr.Id, curr.Name, "text",
                        prev.Text ?? string.Empty,
                        curr.Text ?? string.Empty,
                        PatchChangeDirection.Neutral));
                }
            }
            return result;
        }

        private static void AddStatChange(
            List<PatchChange> dst,
            CardSnapshot card,
            string fieldName,
            int? from,
            int? to,
            Func<int, int, PatchChangeDirection> classify)
        {
            if (from == to) return;
            var direction = (from.HasValue && to.HasValue)
                ? classify(from.Value, to.Value)
                : PatchChangeDirection.Neutral;
            dst.Add(new PatchChange(
                card.Id, card.Name, fieldName,
                from?.ToString() ?? string.Empty,
                to?.ToString() ?? string.Empty,
                direction));
        }

        // Cost: higher = worse for player = Nerf. Lower = cheaper = Buff.
        private static PatchChangeDirection CostDirection(int from, int to)
            => to > from ? PatchChangeDirection.Nerf
             : to < from ? PatchChangeDirection.Buff
             : PatchChangeDirection.Neutral;

        // Attack / health / durability / armor: higher = better for player = Buff. Lower = Nerf.
        private static PatchChangeDirection StatDirection(int from, int to)
            => to > from ? PatchChangeDirection.Buff
             : to < from ? PatchChangeDirection.Nerf
             : PatchChangeDirection.Neutral;
    }
}
