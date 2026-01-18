using ConnectionManager.Contracts;
using ConnectionManager.DTO;
using HtmlAgilityPack;
using Polly;
using Polly.Retry;
using System.Net;

namespace ConnectionManager;

public delegate void RetryNotificationHandler(
    string message, int statusCode, int retryCount,  
    int retryLeft, TimeSpan delay);

public class NautaConnection : IHttpConnection
{
    private readonly HttpClient _httpClient;    
    private readonly CookieContainer _cookieContainer;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public event RetryNotificationHandler? OnRetryOccurred;
    private const int RetryCount = 3;

    public NautaConnection(HttpClient httpClient, CookieContainer cookieContainer)
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
        Action<HttpRequestMessage>? configureRequest = null,
        CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(
                async (CancellationToken token) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                configureRequest?.Invoke(request);

                return await _httpClient.SendAsync(request, token);                    
            }, ct);

            return await ProcessResponse(response, url);
        }       
        catch(OperationCanceledException)
        {
            return new HttpResponse { Status = 499, Message = "Operación cancelada o tiempo de espera agotado." };
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }        

    public async Task<HttpResponse> Post(
        string url, 
        Dictionary<string, string> data, 
        Action<HttpRequestMessage>? configureRequest = null,
        CancellationToken ct= default)
    {
        try
        {
            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(
                async (CancellationToken token) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new FormUrlEncodedContent(data);                
                configureRequest?.Invoke(request);

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

    private HttpResponse HandleException(Exception ex)
    {
        var(status, message) = ex switch
        {
            HttpRequestException hrex => (hrex.StatusCode != null ? (int)hrex.StatusCode : 500, ex.Message),
            TaskCanceledException => ((int)HttpStatusCode.RequestTimeout, "El servidor está tardando mucho en responder."),
            _ => (500, "Error inesperado.")
        };

        return new HttpResponse { Status = status, Message = message};
    }

    private async Task<HttpResponse> ProcessResponse(
        HttpResponseMessage response, string originalUrl)
    {
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();

        var fields = response.Content.Headers.ContentType?.MediaType == "text/html"
            ? ParseHtmlFields(content) : [];

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
            Response = content,
            UrlRedirect = response.RequestMessage?.RequestUri?.ToString() ?? originalUrl,
            Headers = headers,
            Cookies = cookies,
            FormFields = fields
        };
    }

    private Dictionary<string, string> ParseHtmlFields(string content)
    {
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(content);

        var nodes = htmlDoc.DocumentNode.SelectNodes($"//input[@name]");
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                string name = node.GetAttributeValue("name", "");
                string value = node.GetAttributeValue("value", "");
                if (!string.IsNullOrEmpty(name))
                    fields[name] = value;
            }
        }

        return fields;
    }
}
