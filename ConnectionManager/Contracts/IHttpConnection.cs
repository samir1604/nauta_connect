using ConnectionManager.DTO;
using ConnectionManager.Results;


namespace ConnectionManager.Contracts
{
    public interface IHttpConnection
    {
        event RetryNotificationHandler? OnRetryOccurred;

        Task<Result<HttpResponse>> Get(
            string url,
            Action<IRequestConfiguration>? config = null,
            CancellationToken ct = default);

        Task<Result<HttpResponse>> Post(
            string url,
            IRequestContent? data = null,
            Action<IRequestConfiguration>? config = null,
            CancellationToken ct = default);
    }
}
