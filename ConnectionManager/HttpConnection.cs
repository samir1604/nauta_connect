using ConnectionManager.DTO;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager
{
    public class HttpConnection
    {
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;

        public HttpConnection() 
        {
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,                
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true                
            };

            _httpClient = new HttpClient(handler);
        }

        public async Task<HttpResponse> Get(string url, Dictionary<string, string>? customHeaders = null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if(customHeaders != null)                
                    foreach(var header in customHeaders)                    
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);                    

                var response = await _httpClient.SendAsync(request);                
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();

                var fields = response.Content.Headers.ContentType?.MediaType == "text/html" 
                    ? ParseHtmlFields(content) : [];
                                
                var cookies = _cookieContainer.GetCookies(new Uri(url))
                    .Cast<Cookie>().ToDictionary(n => n.Name, v => v.Value);

                var headers = response.Headers.ToDictionary(k => k.Key, v => v.Value.ToArray());

                return new HttpResponse
                {
                    Status = (int)response.StatusCode,
                    Response = content,
                    UrlRedirect = response.RequestMessage?.RequestUri?.ToString() ?? url,
                    Headers = headers,
                    Cookies = cookies,
                    FormFields = fields                    
                };
            }
            catch(HttpRequestException ex)
            {
                return new HttpResponse
                {
                    Status = ex.StatusCode != null ? (int)ex.StatusCode : 500,
                    Message = ex.Message,                    
                };
            }
            catch(TaskCanceledException ex)
            {
                return new HttpResponse
                {
                    Status = (int)HttpStatusCode.RequestTimeout,
                    Message = ex.Message,
                };
            }
            catch (Exception ex)
            {
                return new HttpResponse
                {
                    Status = 500,
                    Message = ex.Message,
                };
            }
        }

        private Dictionary<string, string> ParseHtmlFields(string content)
        {
            Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);

            var nodes = htmlDoc.DocumentNode.SelectNodes($"//input[@name]");
            if(nodes != null)
            {
                foreach(var node in nodes)
                {
                    string name = node.GetAttributeValue("name", "");
                    string value = node.GetAttributeValue("value", "");
                    if (!string.IsNullOrEmpty(name))
                        fields[name] = value;
                }
            }

            return fields;
        }

        public async Task<HttpResponse> Post(string url, Dictionary<string, string>? customHeaders, Dictionary<string, string> data)
        {
            try
            {
                var content = new FormUrlEncodedContent(data);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);                

                if (customHeaders != null)
                    foreach (var header in customHeaders)
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string body = await response.Content.ReadAsStringAsync();

                return new HttpResponse
                {
                    Status = (int)response.StatusCode,
                    Response = body,
                    UrlRedirect = response.RequestMessage?.RequestUri?.ToString() ?? url,                    
                };
            }
            catch (HttpRequestException ex)
            {
                return new HttpResponse
                {
                    Status = ex.StatusCode != null ? (int)ex.StatusCode : 500,
                    Message = ex.Message,
                };
            }
            catch (TaskCanceledException ex)
            {
                return new HttpResponse
                {
                    Status = (int)HttpStatusCode.RequestTimeout,
                    Message = ex.Message,
                };
            }
            catch (Exception ex)
            {
                return new HttpResponse
                {
                    Status = 500,
                    Message = ex.Message,
                };
            }
        }
    }
}
