using ConnectionManager.Contracts;
using ConnectionManager.DTO;
using ConnectionManager.Internal;
using HtmlAgilityPack;
using Polly;
using Polly.Retry;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;

namespace ConnectionManager;

public delegate void RetryNotificationHandler(
    string message, int statusCode, int retryCount,  
    int retryLeft, TimeSpan delay);

public class HttpConnection : IHttpConnection
{
    private readonly HttpClient _httpClient;    
    private readonly CookieContainer _cookieContainer;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public event RetryNotificationHandler? OnRetryOccurred;
    private const int RetryCount = 3;

    public HttpConnection(HttpClient httpClient, CookieContainer cookieContainer)
    {
        _httpClient = httpClient;
        _cookieContainer = cookieContainer;

        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >=500)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(RetryCount, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (result, timeSpan, retryCount, context) => {

                    string message = result.Exception?.Message ?? result.Result?.ReasonPhrase ?? "";
                    int statusCode = result.Result != null ? (int) result.Result.StatusCode : 0;

                    OnRetryOccurred?.Invoke(message, statusCode, RetryCount, retryCount, timeSpan);
                }
            );
    }

    public async Task<HttpResponse> Get(
        string url, 
        Action<IRequestConfiguration>? config = null,
        CancellationToken ct = default) =>
                await SendInternalAsync(HttpMethod.Get, url, null, config, ct);

    public async Task<HttpResponse> Post(
        string url, 
        IRequestContent? data = null, 
        Action<IRequestConfiguration>? config = null,
        CancellationToken ct= default) =>
         await SendInternalAsync(HttpMethod.Post, url, data, config, ct);    

    private async Task<HttpResponse> SendInternalAsync(
        HttpMethod method,
        string url,
        IRequestContent? content = null,
        Action<IRequestConfiguration>? config = null,
        CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(
                async (CancellationToken token) =>
                {
                    using var request = new HttpRequestMessage(method, url);
                    config?.Invoke(new RequestConfigurationBuilder(request));

                    if (content != null)
                    {
                        request.Content = MapContent(content);
                    }

                    return await _httpClient.SendAsync(request, token);
                    
                }, ct);

            return await ProcessResponse(response, url);
        }
        catch (OperationCanceledException)
        {
            return new HttpResponse { Status = 499, Message = "Operación cancelada o tiempo de espera agotado." };
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private HttpContent MapContent(IRequestContent content) => 
        content.Type switch
    {
        HttpContentType.Form => new FormUrlEncodedContent((Dictionary<string, string>)content.RawData),
        HttpContentType.Json => new StringContent(
            JsonSerializer.Serialize(content.RawData), System.Text.Encoding.UTF8, "application/json"),
        _ => throw new NotImplementedException()
    };    

    private async Task<HttpResponse> ProcessResponse(
        HttpResponseMessage response, string originalUrl)
    {
        string rawContent = await response.Content.ReadAsStringAsync();        

        var cookies = _cookieContainer
            .GetCookies(
                new Uri(response.RequestMessage?.RequestUri?.ToString() ?? originalUrl))
            .Cast<Cookie>()
            .ToDictionary(n => n.Name, v => v.Value);

        var headers = response.Headers
            .ToDictionary(k => k.Key, v => v.Value.ToArray());

        return new HttpResponse
        {
            Status = (int)response.StatusCode,
            RawContent = rawContent,
            UrlRedirect = response.RequestMessage?.RequestUri?.ToString() ?? originalUrl,
            Headers = headers,
            Cookies = cookies
        };
    }

    private HttpResponse HandleException(Exception ex)
    {
        var (status, message) = ex switch
        {
            HttpRequestException hrex => (hrex.StatusCode != null ? (int)hrex.StatusCode : 500, ex.Message),
            TaskCanceledException => ((int)HttpStatusCode.RequestTimeout, "El servidor está tardando mucho en responder."),
            _ => (500, "Error inesperado.")
        };

        return new HttpResponse { Status = status, Message = message };
    }
}
