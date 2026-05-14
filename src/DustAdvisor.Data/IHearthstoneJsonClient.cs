using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Data
{
    public interface IHearthstoneJsonClient
    {
        Task<IReadOnlyList<CardMeta>> LoadCollectibleAsync(string locale, CancellationToken ct);
    }
}
