using System;
using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    public sealed class DataLoader
    {
        private readonly IHearthstoneJsonClient _hsj;
        private readonly UncraftableRepository _uncraftableRepo;
        private readonly RefundRepository _refundRepo;

        public DataLoader(IHearthstoneJsonClient hsj, UncraftableRepository uncraftableRepo, RefundRepository refundRepo)
        {
            _hsj = hsj;
            _uncraftableRepo = uncraftableRepo;
            _refundRepo = refundRepo;
        }

        public async Task<DataSnapshot> LoadAsync(
            string locale, string uncraftablePath, string refundPath, DateTimeOffset now, CancellationToken ct)
        {
            var meta = await _hsj.LoadCollectibleAsync(locale, ct).ConfigureAwait(false);
            var uncraftable = _uncraftableRepo.Load(uncraftablePath);
            var refund = _refundRepo.Load(refundPath, now);
            return new DataSnapshot(meta, uncraftable, refund);
        }
    }
}
