using System.Collections.Generic;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Data
{
    public sealed class DataSnapshot
    {
        public IReadOnlyList<CardMeta> Meta { get; }
        public IReadOnlyCollection<(string CardId, Premium Premium)> Uncraftable { get; }
        public IReadOnlyCollection<string> RefundWindow { get; }
        public IReadOnlyDictionary<string, string> MetaTiers { get; }

        public DataSnapshot(
            IReadOnlyList<CardMeta> meta,
            IReadOnlyCollection<(string, Premium)> uncraftable,
            IReadOnlyCollection<string> refundWindow,
            IReadOnlyDictionary<string, string> metaTiers = null)
        {
            Meta = meta;
            Uncraftable = uncraftable;
            RefundWindow = refundWindow;
            MetaTiers = metaTiers ?? new Dictionary<string, string>();
        }
    }
}
