using ConnectionManager.Contracts;
using ConnectionManager.DTO;
using ConnectionManager.Internal;
using ConnectionManager.Result;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Sockets;
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

    public async Task<Result<HttpResponse>> Get(
        string url, 
        Action<IRequestConfiguration>? config = null,
        CancellationToken ct = default) =>
                await SendInternalAsync(HttpMethod.Get, url, null, config, ct);

    public async Task<Result<HttpResponse>> Post(
        string url, 
        IRequestContent? data = null, 
        Action<IRequestConfiguration>? config = null,
        CancellationToken ct= default) =>
         await SendInternalAsync(HttpMethod.Post, url, data, config, ct);    

    private async Task<Result<HttpResponse>> SendInternalAsync(
        HttpMethod method,
        string url,
        IRequestContent? content = null,
        Action<IRequestConfiguration>? config = null,
        CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _retryPolicy
                .ExecuteAsync(async token =>
                {
                    using var request = new HttpRequestMessage(method, url);
                    config?.Invoke(new RequestConfigurationBuilder(request));

                    if (content != null) request.Content = MapContent(content);                   

                    return await _httpClient.SendAsync(request, token);
                    
                }, ct);

            if (!response.IsSuccessStatusCode)
                return HttpFailures.UnexpectedResponse<HttpResponse>(
                    "El servidor de ETECSA está devolviendo un error técnico.",
                    $"Estado: {(int)response.StatusCode}");
            
            var processed = await ProcessResponse(response, url);
            return Result<HttpResponse>.Success(processed);
        }        
        catch (Exception ex)
        {
            return HandleException(ex, ct);
        }
    }

    private static HttpContent MapContent(IRequestContent content) => 
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
            RawContent = rawContent,
            UrlRedirect = response.RequestMessage?.RequestUri?.ToString() ?? originalUrl,
            Headers = headers,
            Cookies = cookies
        };
    }

    private static Result<HttpResponse> HandleException(Exception ex, CancellationToken ct)
    {
        return ex switch
        {
            OperationCanceledException or TaskCanceledException when ct.IsCancellationRequested =>
                HttpFailures.NetworkError<HttpResponse>(
                    "Operación cancelada por el usuario."),
            OperationCanceledException or TaskCanceledException =>
                HttpFailures.NetworkError<HttpResponse>(
                    "Tiempo de espera agotado. La conexión es muy lenta."),
            HttpRequestException hrex when hrex.InnerException is SocketException sockEx =>
                sockEx.SocketErrorCode switch
                {
                    SocketError.HostNotFound =>
                        HttpFailures.NetworkError<HttpResponse>(
                            "No se encuentra el portal. Verifica tu conexión Wi-Fi."),
                    SocketError.ConnectionRefused =>
                    HttpFailures.NetworkError<HttpResponse>(
                            "El portal de ETECSA rechazó la conexión."),
                    _ => HttpFailures.NetworkError<HttpResponse>(
                            $"Error de red: {sockEx.SocketErrorCode}"),
                },
            _ => HttpFailures.UnexpectedResponse<HttpResponse>(
                    "Ocurrió un error inesperado al intentar comunicar con el servidor.")
        };
    }
}
