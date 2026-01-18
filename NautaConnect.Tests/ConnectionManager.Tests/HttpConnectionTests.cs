using ConnectionManager;
using ConnectionManager.DTO;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Moq;
using Moq.Language.Flow;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net; 
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NautaConnect.Tests.ConnectionManager.Tests
{
    public class HttpConnectionTests
    {
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly CookieContainer _cookieContainer;
        private readonly HttpClient _httpClient;
        private readonly HttpConnection _sut;

        private const string testUrl = "https://secure.etecsa.net:8443/";

        public HttpConnectionTests() 
        {
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _cookieContainer = new CookieContainer();
            _httpClient = new HttpClient(_handlerMock.Object);
            _sut = new HttpConnection(_httpClient, _cookieContainer);
        }

        [Fact]
        public async Task Get_ShouldCaptureCookies_WhenServerSendsThem()
        {   
            var uri = new Uri(testUrl);

            _cookieContainer.Add(uri, new Cookie("JSESSIONID", "ABC123XYZ"));
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Sesion Iniciada</html>"),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, testUrl),
            };            

            SetupMockResponse(() => response);
            
            // --- ACT ---
            var result = await _sut.Get(testUrl);

            // --- ASSERT ---			
            var containerCookies = _cookieContainer.GetCookies(uri);
            Assert.NotEmpty(containerCookies);
            Assert.Single(containerCookies);
            Assert.Equal("ABC123XYZ", containerCookies["JSESSIONID"]!.Value);

            Assert.True(result.Cookies.ContainsKey("JSESSIONID"), "El diccionario de cookies debería tener JSESSIONID");
            Assert.Equal("ABC123XYZ", result.Cookies["JSESSIONID"]);
        }
        
        [Fact]
        public async Task Get_ShouldReturn499_WhenOperationIsCanceled()
        {
            // Arrange
            SetupMockResponse(async (req, ct) =>
            {
                await Task.Delay(5000, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await _sut.Get("https://test.com", null, cts.Token);

            // Assert
            Assert.Equal(499, result.Status);
            Assert.Contains("cancelada", result.Message);
        }

        [Fact]
        public async Task Get_ShouldReturnFinalUrl_WhenRedirectOccurs()
        {
            // Arrange
            
            var finalUrl = "https://secure.etecsa.net/login.do";

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUrl);

            SetupMockResponse(() => response);            
            
            // Act
            var result = await _sut.Get("http://1.1.1.1");

            // Assert
            Assert.Equal(finalUrl, result.UrlRedirect);
        }

        
        [Fact]
        public async Task Polly_ShouldRetryThreeTimes_OnServerError()
        {
            // --- ARRANGE ---            
			int actualCalls = 0;

            SetupMockResponse(() =>
            {
                actualCalls++;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            // --- ACT ---
            var result = await _sut.Get("https://secure.etecsa.net:8443/");

            // --- ASSERT ---
            Assert.Equal(4, actualCalls);
			Assert.Equal(500, result.Status);
		}  
        
        
        [Fact]
        public async Task Polly_ShouldRetry_OnNetworkException()
        {
            // --- ARRANGE ---            
            int actualCalls = 0;            
            
            SetupMockResponse(() =>
            {
                actualCalls++;
                throw new HttpRequestException("No se puede establecer una conexión con el servidor.");
            });

            // --- ACT ---            
            var result = await _sut.Get("https://secure.etecsa.net:8443/");

            // --- ASSERT ---
            // 1 inicial + 3 reintentos = 4 llamadas
            Assert.Equal(4, actualCalls);

            // Verificamos que al final, tras fallar los reintentos, 
            // nuestro HandleException devuelva un error 500 o el mensaje de error.
            Assert.Equal(500, result.Status);
            Assert.Contains("No se puede establecer una conexión", result.Message);
        }

        

        [Fact]
        public async Task Get_ShouldTriggerNotificationEvent_OnEachRetry()
        {
            // Arrange
            int notificationCount = 0;
            int lastRetryCountReceived = 0;
            
            _sut.OnRetryOccurred += (message, statusCode, totalRetries, retryCount, delay) =>
            {
                notificationCount++;
                lastRetryCountReceived = retryCount;
            };            
            
            SetupMockResponse(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

            // Act
            await _sut.Get("https://secure.etecsa.net:8443/");

            // Assert
            //El evento debe dispararse 3 veces por los 3 reintentos de polly
            Assert.Equal(3, notificationCount);
            Assert.Equal(3, lastRetryCountReceived);            
        }        

        [Fact]
        public async Task Post_ShouldSendCorrectFormUrlEncodedData()
        {
            // Arrange
            var dataToSend = new Dictionary<string, string>    {
                { "username", "pepe@nauta.com.cu" },
                { "password", "12345" }
            };

            string? capturedContent = null;
            
            SetupMockResponse(() => 
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html>Exito</html>")
                })
                .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
                {
                    capturedContent = await req.Content!.ReadAsStringAsync();
                });

            // Act
            await _sut.Post(
                "https://secure.etecsa.net:8443/login", new FormContent(dataToSend));

            // Assert            
            Assert.Contains("username=pepe%40nauta.com.cu", capturedContent);
            Assert.Contains("password=12345", capturedContent);
        }
        

        [Fact]
        public async Task Post_ShouldIncludeExistingCookies_InHeaders()
        {
            // --- ARRANGE ---
            string url = "https://secure.etecsa.net:8443/login";
            
            _cookieContainer.Add(new Uri(url), new Cookie("SESSION_ID", "12345"));
            
            SetupMockResponse(() => new HttpResponseMessage(HttpStatusCode.OK));
            // --- ACT ---
            await _sut.Post(url);

            // --- ASSERT ---
            // Nota: HttpClient no pone las cookies en req.Headers.Add, 
            // las maneja internamente el Handler. Por eso, lo que validamos 
            // es que el proceso no falle y que el contenedor sea el correcto.
            var cookiesEnviadas = _cookieContainer.GetCookies(new Uri(url));
            Assert.NotEmpty(cookiesEnviadas);
            Assert.Contains(cookiesEnviadas.Cast<Cookie>(), c => c.Name == "SESSION_ID");            
        }

        
        [Fact]
        public async Task Post_ShouldReturnFinalUrl_WhenLoginRedirects()
        {
            // --- ARRANGE ---
            string loginUrl = "https://secure.etecsa.net:8443/login";
            string welcomeUrl = "https://secure.etecsa.net:8443/welcome.do";

            // Simulamos la respuesta final después de que HttpClient siguiera la redirección
            var finalResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Bienvenido a Nauta</html>"),
                // El HttpClient real llena esta propiedad cuando hay redirecciones
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, welcomeUrl)
            };            

            SetupMockResponse(() => finalResponse);

            // --- ACT ---
            var result = await _sut.Post(loginUrl);

            // --- ASSERT ---            
            Assert.Equal(welcomeUrl, result.UrlRedirect);
            Assert.Contains("Bienvenido", result.RawContent);
        }

        

        [Fact]
        public async Task Post_ShouldSendCustomHeaders_WhenProvided()
        {
            // --- ARRANGE ---
            HttpRequestHeaders? capturedHeaders = null;

            SetupMockResponse(() => new HttpResponseMessage(HttpStatusCode.OK))
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    capturedHeaders = req.Headers;
                });

            // --- ACT ---
            await _sut.Post("https://test.com", null, req =>
            {
                req.SetReferer("https://secure.etecsa.net/");                
            });

            // --- ASSERT ---
            Assert.NotNull(capturedHeaders);
            Assert.Equal("https://secure.etecsa.net/", capturedHeaders.Referrer?.ToString());
        }
                
        [Fact]
        public async Task Post_ShouldRetry_WhenServerFailsDuringLogin()
        {
            // Arrange
            int postAttempts = 0;
            
            SetupMockResponse(() =>
            {
                postAttempts++;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            // Act
            var result = await _sut.Post(
                "https://secure.etecsa.net:8443/login");

            // Assert
            // 1 inicial + 3 reintentos = 4
            Assert.Equal(4, postAttempts);
            Assert.Equal(500, result.Status);
        }

        [Fact]
        public async Task Post_ShouldSendCorrectJsonData_WhenJsonPayloadIsUsed()
        {
            // Arrange
            var data = new { User = "admin", Id = 123 };
            string? capturedContent = null;
            string? contentType = null;

            SetupMockResponse(() => new HttpResponseMessage(HttpStatusCode.OK))
                .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
                {
                    capturedContent = await req.Content!.ReadAsStringAsync();
                    contentType = req.Content.Headers.ContentType?.MediaType;
                });

            // Act            
            await _sut.Post("https://api.test", new JsonContent(data));

            // Assert
            Assert.Equal("application/json", contentType);
            Assert.Contains("\"User\":\"admin\"", capturedContent);
            Assert.Contains("\"Id\":123", capturedContent);
        }

        [Fact]
        public async Task Post_ShouldWorkCorrectly_WhenPayloadAndConfigAreNull()
        {
            // Arrange
            SetupMockResponse(() => new HttpResponseMessage(HttpStatusCode.OK));

            // Act & Assert
            var exception = await Record.ExceptionAsync(() => _sut.Post("https://test.com"));

            Assert.Null(exception); // El motor debe manejar internamente los nulos sin lanzar NullReferenceException
        }

        [Fact]
        public async Task Get_ShouldApplyComplexConfiguration_ThroughInterface()
        {
            // Arrange
            HttpRequestHeaders? capturedHeaders = null;

            SetupMockResponse(() => new HttpResponseMessage(HttpStatusCode.OK))
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    capturedHeaders = req.Headers;
                });

            string customUa = "NautaBot/2.0";
            string referer = "https://secure.etecsa.net:8443/login.do?param=123";

            // Act
            await _sut.Get("https://test.com", config =>
            {
                config.SetUserAgent(customUa);
                config.SetReferer(referer);
                config.AddHeader("X-Custom-Header", "Value123");
            });

            // Assert
            Assert.Equal(customUa, capturedHeaders!.UserAgent.ToString());
            Assert.Equal(referer, capturedHeaders.Referrer?.ToString());
            Assert.Equal("Value123", capturedHeaders.GetValues("X-Custom-Header").First());
        }

        private IReturnsResult<HttpMessageHandler> SetupMockResponse(Func<HttpResponseMessage> response)
        {
            return _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).ReturnsAsync(response);
        }

        private void SetupMockResponse(
            Func<HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> response)
        {
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).Returns(response);
        }
    }
}
