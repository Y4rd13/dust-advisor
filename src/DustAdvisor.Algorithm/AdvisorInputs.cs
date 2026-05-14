using System.Collections.Generic;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm
{
    public sealed class AdvisorInputs
    {
        public IReadOnlyList<CollectionEntry> Collection { get; }
        public IReadOnlyList<CardMeta> Meta { get; }
        public IReadOnlyCollection<(string CardId, Premium Premium)> Uncraftable { get; }
        public IReadOnlyCollection<string> RefundWindow { get; }
        public IReadOnlyDictionary<string, int> DeckUsage { get; }
        public IReadOnlyCollection<string> RotatingCardIds { get; }
        public AdvisorOptions Options { get; }

        public AdvisorInputs(
            IReadOnlyList<CollectionEntry> collection,
            IReadOnlyList<CardMeta> meta,
            IReadOnlyCollection<(string, Premium)> uncraftable,
            IReadOnlyCollection<string> refundWindow,
            AdvisorOptions options,
            IReadOnlyDictionary<string, int> deckUsage = null,
            IReadOnlyCollection<string> rotatingCardIds = null)
        {
            Collection = collection;
            Meta = meta;
            Uncraftable = uncraftable;
            RefundWindow = refundWindow;
            Options = options;
            DeckUsage = deckUsage ?? new Dictionary<string, int>();
            RotatingCardIds = rotatingCardIds ?? new HashSet<string>();
        }
    }
}
