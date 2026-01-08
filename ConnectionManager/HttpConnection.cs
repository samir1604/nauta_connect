using ConnectionManager.DTO;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager
{
    public class HttpConnection
    {
        private static HttpClient _httpClient;
        private static CookieContainer _cookieContainer;

        static HttpConnection() 
        {
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,                
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true                
            };

            _httpClient = new HttpClient(handler);
        }

        public async Task<HttpResponse> Get(string url, Action<HttpRequestHeaders>? configureHeaders = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                configureHeaders?.Invoke(request.Headers);
                
                var response = await _httpClient.SendAsync(request);
                return await ProcessResponse(response, url);
            }            
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }        

        public async Task<HttpResponse> Post(
            string url, Dictionary<string, string> data, Action<HttpRequestMessage>? configureHeaders = null)
        {
            try
            {                
                using var request = new HttpRequestMessage(HttpMethod.Post, url);                
                request.Content = new FormUrlEncodedContent(data);
                configureHeaders?.Invoke(request);                

                var response = await _httpClient.SendAsync(request);
                return await ProcessResponse(response, url);                
            }            
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        private HttpResponse HandleException(Exception ex)
        {
            int status = ex switch
            {
                HttpRequestException hrex => hrex.StatusCode != null ? (int)hrex.StatusCode : 500,
                TaskCanceledException => (int)HttpStatusCode.RequestTimeout,
                _ => 500
            };

            return new HttpResponse { Status = status, Message = ex.Message };
        }

        private async Task<HttpResponse> ProcessResponse(
            HttpResponseMessage response, string originalUrl)
        {
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            var fields = response.Content.Headers.ContentType?.MediaType == "text/html"
                ? ParseHtmlFields(content) : [];

            var cookies = _cookieContainer
                .GetCookies(new Uri(originalUrl))
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
}
