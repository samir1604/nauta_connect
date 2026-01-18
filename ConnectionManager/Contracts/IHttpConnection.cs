using ConnectionManager.DTO;

namespace ConnectionManager.Contracts
{
    public interface IHttpConnection
    {
        event RetryNotificationHandler? OnRetryOccurred;

        Task<HttpResponse> Get(
            string url,
            Action<IRequestConfiguration>? config = null,
            CancellationToken ct = default);

        Task<HttpResponse> Post(
            string url,
            IRequestContent? data = null,
            Action<IRequestConfiguration>? config = null,
            CancellationToken ct = default);
    }
}
