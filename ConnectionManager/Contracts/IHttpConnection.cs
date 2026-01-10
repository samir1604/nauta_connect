using ConnectionManager.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager.Contracts
{
    public interface IHttpConnection
    {
        event RetryNotificationHandler? OnRetryOccurred;

        Task<HttpResponse> Get(
            string url,
            Action<HttpRequestMessage>? configureRequest = null,
            CancellationToken ct = default);

        Task<HttpResponse> Post(
            string url,
            Dictionary<string, string> data,
            Action<HttpRequestMessage>? configureRequest = null,
            CancellationToken ct = default);
    }
}
