using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    public interface IHttpFetcher
    {
        Task<string> GetAsync(string url, CancellationToken ct);
    }
}
