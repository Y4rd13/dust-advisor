using System;
using System.Collections.Generic;
using System.Linq;

namespace DustAdvisor.Data
{
    /// <summary>
    /// One entry in refund_auto.json — a card that changed in a recent balance patch,
    /// with the field-level changes that triggered detection. Persisted/loaded by
    /// RefundAutoRepository; consumed by the UI for the Flag-column tooltip and the
    /// Hide-buffs filter.
    /// </summary>
    public sealed class RefundEntry
    {
        public string CardId { get; }
        public string CardName { get; }
        public DateTimeOffset ExpiresUtc { get; }
        public DateTimeOffset DetectedAt { get; }
        public IReadOnlyList<PatchChange> Changes { get; }
        public PatchChangeDirection AggregateDirection { get; }

        public RefundEntry(
            string cardId,
            string cardName,
            DateTimeOffset expiresUtc,
            DateTimeOffset detectedAt,
            IReadOnlyList<PatchChange> changes)
        {
            CardId = cardId;
            CardName = cardName;
            ExpiresUtc = expiresUtc;
            DetectedAt = detectedAt;
            Changes = changes ?? new List<PatchChange>();
            AggregateDirection = ComputeAggregateDirection(Changes);
        }

        private static PatchChangeDirection ComputeAggregateDirection(IReadOnlyList<PatchChange> changes)
        {
            // If ANY change is a nerf, treat the card overall as a nerf — that's the
            // direction the player cares about for refund decisions. Pure buffs are flagged
            // Buff (user can hide them). Text-only changes are Neutral.
            if (changes.Any(c => c.Direction == PatchChangeDirection.Nerf)) return PatchChangeDirection.Nerf;
            if (changes.Any(c => c.Direction == PatchChangeDirection.Buff)) return PatchChangeDirection.Buff;
            return PatchChangeDirection.Neutral;
        }
    }
}
