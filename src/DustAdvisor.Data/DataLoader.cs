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
        private readonly MetaTierRepository _metaTierRepo;
        private readonly RefundAutoRepository _refundAutoRepo;

        public DataLoader(IHearthstoneJsonClient hsj, UncraftableRepository uncraftableRepo, RefundRepository refundRepo, NeverSuggestRepository neverSuggestRepo, MetaTierRepository metaTierRepo, RefundAutoRepository refundAutoRepo = null)
        {
            _hsj = hsj;
            _uncraftableRepo = uncraftableRepo;
            _refundRepo = refundRepo;
            _neverSuggestRepo = neverSuggestRepo;
            _metaTierRepo = metaTierRepo;
            _refundAutoRepo = refundAutoRepo ?? new RefundAutoRepository();
        }

        public async Task<DataSnapshot> LoadAsync(
            string locale,
            string uncraftablePath,
            string refundPath,
            string neverSuggestPath,
            string metaTiersPath,
            DateTimeOffset now,
            CancellationToken ct,
            string refundAutoPath = null)
        {
            var meta = await _hsj.LoadCollectibleAsync(locale, ct).ConfigureAwait(false);
            var heuristic = await _hsj.LoadHeuristicUncraftableAsync(locale, ct).ConfigureAwait(false);
            var curated = _uncraftableRepo.Load(uncraftablePath);
            var neverSuggest = _neverSuggestRepo.Load(neverSuggestPath);

            var merged = new HashSet<(string CardId, Premium Premium)>();
            foreach (var c in curated) merged.Add(c);
            foreach (var h in heuristic) merged.Add(h);
            foreach (var n in neverSuggest) merged.Add(n);

            var manualRefund = _refundRepo.Load(refundPath, now);
            var refundUnion = new HashSet<string>(manualRefund);
            var refundDetails = new Dictionary<string, RefundEntry>();

            if (!string.IsNullOrEmpty(refundAutoPath))
            {
                foreach (var entry in _refundAutoRepo.Load(refundAutoPath, now))
                {
                    refundUnion.Add(entry.CardId);
                    refundDetails[entry.CardId] = entry;
                }
            }

            var tiers = _metaTierRepo.Load(metaTiersPath);
            return new DataSnapshot(meta, merged, refundUnion, tiers, refundDetails);
        }
    }
}
