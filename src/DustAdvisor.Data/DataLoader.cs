using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Data
{
    public sealed class DataLoader
    {
        private readonly IHearthstoneJsonClient _hsj;
        private readonly UncraftableRepository _uncraftableRepo;
        private readonly RefundRepository _refundRepo;
        private readonly NeverSuggestRepository _neverSuggestRepo;

        public DataLoader(IHearthstoneJsonClient hsj, UncraftableRepository uncraftableRepo, RefundRepository refundRepo, NeverSuggestRepository neverSuggestRepo)
        {
            _hsj = hsj;
            _uncraftableRepo = uncraftableRepo;
            _refundRepo = refundRepo;
            _neverSuggestRepo = neverSuggestRepo;
        }

        public async Task<DataSnapshot> LoadAsync(
            string locale,
            string uncraftablePath,
            string refundPath,
            string neverSuggestPath,
            DateTimeOffset now,
            CancellationToken ct)
        {
            var meta = await _hsj.LoadCollectibleAsync(locale, ct).ConfigureAwait(false);
            var heuristic = await _hsj.LoadHeuristicUncraftableAsync(locale, ct).ConfigureAwait(false);
            var curated = _uncraftableRepo.Load(uncraftablePath);
            var neverSuggest = _neverSuggestRepo.Load(neverSuggestPath);

            var merged = new HashSet<(string CardId, Premium Premium)>();
            foreach (var c in curated) merged.Add(c);
            foreach (var h in heuristic) merged.Add(h);
            foreach (var n in neverSuggest) merged.Add(n);

            var refund = _refundRepo.Load(refundPath, now);
            return new DataSnapshot(meta, merged, refund);
        }
    }
}
