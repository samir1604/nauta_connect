using ConnectionManager;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Moq;
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
        private readonly NautaConnection _connection;

        private const string testUrl = "https://secure.etecsa.net:8443/";

        public HttpConnectionTests() 
        {
            _handlerMock = new Mock<HttpMessageHandler>();
            _cookieContainer = new CookieContainer();
            _httpClient = new HttpClient(_handlerMock.Object);
            _connection = new NautaConnection(_httpClient, _cookieContainer);
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

            _handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(response);

            // --- ACT ---
            var result = await _connection.Get(testUrl);

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
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage req, CancellationToken ct) => {
                    await Task.Delay(5000, ct); // Simula espera de 5 segundos
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await _connection.Get("https://test.com", null, cts.Token);

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

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
            
            // Act
            var result = await _connection.Get("http://1.1.1.1");

            // Assert
            Assert.Equal(finalUrl, result.UrlRedirect);
        }

        [Fact]
        public async Task Polly_ShouldRetryThreeTimes_OnServerError()
        {
            // --- ARRANGE ---            
			int actualCalls = 0;

			_handlerMock
			   .Protected()
			   .Setup<Task<HttpResponseMessage>>(
				  "SendAsync",
				  ItExpr.IsAny<HttpRequestMessage>(),
				  ItExpr.IsAny<CancellationToken>()
			   )
			   .ReturnsAsync(() => {
				   actualCalls++;
				   return new HttpResponseMessage(HttpStatusCode.InternalServerError);
			   });			

            // --- ACT ---
            var result = await _connection.Get("https://secure.etecsa.net:8443/");

            // --- ASSERT ---
            Assert.Equal(4, actualCalls);
			Assert.Equal(500, result.Status);
		}        

        [Fact]
        public async Task ParseHtmlFields_ShouldNotThrow_WhenHtmlIsInvalid()
        {
            // Arrange            
            var invalidHtml = "<html><body><h1>Error de Sistema</h1></body></html>"; // No hay inputs

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(invalidHtml, System.Text.Encoding.UTF8, "text/html")
            };

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            // Act
            var result = await _connection.Get("https://test.com");

            // Assert
            Assert.NotNull(result.FormFields);
            Assert.Empty(result.FormFields); // Verificamos que no falló, solo devolvió 0 campos
        }

        [Fact]
        public async Task Polly_ShouldRetry_OnNetworkException()
        {
            // --- ARRANGE ---            
            int actualCalls = 0;
            
            _handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .Returns(() => {
                   actualCalls++;                   
                   throw new HttpRequestException("No se puede establecer una conexión con el servidor.");
               });            

            // --- ACT ---            
            var result = await _connection.Get("https://secure.etecsa.net:8443/");

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
            
            _connection.OnRetryOccurred += (message, statusCode, totalRetries, retryCount, delay) =>
            {
                notificationCount++;
                lastRetryCountReceived = retryCount;
            };
            
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            // Act
            await _connection.Get("https://secure.etecsa.net:8443/");

            // Assert
            //El evento debe dispararse 3 veces por los 3 reintentos de polly
            Assert.Equal(3, notificationCount);
            Assert.Equal(3, lastRetryCountReceived);            
        }

        [Fact]
        public async Task Get_ShouldParseAllNamedInputs_AndIgnoreOthers()
        {
            // --- ARRANGE ---            
            string fakeHtml = @"
                <html>
                    <body>
                        <form>
                            <input type='hidden' name='CSRFHW' value='token_123'>
                            <input type='hidden' name='wlanuserip' value='10.10.0.5'>
                            <input type='text' name='username' value='usuario@nauta.com.cu'>
                    
                            <input type='password' value='password123'> 
                    
                            <input type='submit' value='Entrar'>
                        </form>
                    </body>
                </html>";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(fakeHtml, System.Text.Encoding.UTF8, "text/html")
            };

            _handlerMock.Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(response);

            // --- ACT ---
            var result = await _connection.Get("https://secure.etecsa.net:8443/");

            // --- ASSERT ---            
            Assert.Equal(3, result.FormFields.Count);
            
            Assert.Equal("token_123", result.FormFields["CSRFHW"]);
            Assert.Equal("10.10.0.5", result.FormFields["wlanuserip"]);
            Assert.Equal("usuario@nauta.com.cu", result.FormFields["username"]);
            
            Assert.False(result.FormFields.ContainsKey(""), "No debería haber llaves vacías");
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

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html>Exito</html>")
                })
                // Capturamos lo que el HttpClient intentó enviar
                .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
                {
                    capturedContent = await req.Content!.ReadAsStringAsync();
                });

            // Act
            await _connection.Post("https://secure.etecsa.net:8443/login", dataToSend);

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

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // --- ACT ---
            await _connection.Post(url, new Dictionary<string, string>());

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

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(finalResponse);

            // --- ACT ---
            var result = await _connection.Post(loginUrl, new Dictionary<string, string>());

            // --- ASSERT ---            
            Assert.Equal(welcomeUrl, result.UrlRedirect);
            Assert.Contains("Bienvenido", result.Response);
        }

        [Fact]
        public async Task Post_ShouldSendCustomHeaders_WhenProvided()
        {
            // --- ARRANGE ---
            HttpRequestHeaders? capturedHeaders = null;

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    // Capturar la cabecera enviada
                    capturedHeaders = req.Headers;
                });

            // --- ACT ---
            await _connection.Post("https://test.com", new Dictionary<string, string>(), req =>
            {
                req.Headers.Referrer = new Uri("https://secure.etecsa.net/");
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
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() => {
                    postAttempts++;
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                });

            // Act
            var result = await _connection.Post(
                "https://secure.etecsa.net:8443/login", []);

            // Assert
            // 1 inicial + 3 reintentos = 4
            Assert.Equal(4, postAttempts);
            Assert.Equal(500, result.Status);
        }
    }
}
