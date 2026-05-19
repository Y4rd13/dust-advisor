using System.Threading;
using System.Threading.Tasks;

namespace DustAdvisor.Data
{
    /// <summary>
    /// HTTP fetcher specialized for patch detection. Exposes HEAD and GET that surface
    /// the Last-Modified header — IHttpFetcher only returns the body, which isn't
    /// enough to know whether the upstream build changed.
    /// </summary>
    public interface IPatchHttpFetcher
    {
        Task<string> HeadLastModifiedAsync(string url, CancellationToken ct);
        Task<(string Body, string LastModified)> GetWithLastModifiedAsync(string url, CancellationToken ct);
    }
}
