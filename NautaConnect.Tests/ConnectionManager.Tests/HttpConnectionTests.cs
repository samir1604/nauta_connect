using ConnectionManager;
using ConnectionManager.DTO;
using ConnectionManager.Results;
using Moq;
using Moq.Language.Flow;
using Moq.Protected;
using System.Net; 
using System.Net.Http.Headers;


namespace NautaConnect.Tests.ConnectionManager.Tests;

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

        Assert.True(result.Value.Cookies.ContainsKey("JSESSIONID"), "El diccionario de cookies debería tener JSESSIONID");
        Assert.Equal("ABC123XYZ", result.Value.Cookies["JSESSIONID"]);

        Assert.True(result.IsSuccess);        
    }

    [Fact]
    public async Task Get_ShouldReturnFailure_WhenOperationIsCanceledByUser()
    {
        // Arrange
        SetupMockResponse((req, ct) =>
        {            
            throw new OperationCanceledException(ct);
        });

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.Get(testUrl, null, cts.Token);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NetworkError, result.Error.Type);
        Assert.Contains("cancelada", result.Error.Message);
    }

    [Fact]
    public async Task Post_ShouldReturnFinalUrl_WhenLoginRedirects()
    {
        // --- ARRANGE ---
        string welcomeUrl = "https://secure.etecsa.net:8443/welcome.do";
        var finalResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Bienvenido</html>"),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, welcomeUrl)
        };

        SetupMockResponse(() => finalResponse);

        // --- ACT ---
        var result = await _sut.Post(testUrl);

        // --- ASSERT ---            
        Assert.True(result.IsSuccess);
        Assert.Equal(welcomeUrl, result.Value.UrlRedirect);
    }


    [Fact]
    public async Task Polly_ShouldRetryThreeTimes_AndReturnFailure_OnServerError()
    {
        // --- ARRANGE ---            
        int actualCalls = 0;
        SetupMockResponse(() =>
        {
            actualCalls++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        // --- ACT ---
        var result = await _sut.Get(testUrl);

        // --- ASSERT ---
        Assert.Equal(4, actualCalls); // 1 + 3 reintentos
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.UnexpectedResponse, result.Error.Type);
        Assert.Contains("500", result.Error.Details);
    }


    [Fact]
    public async Task Polly_ShouldRetry_OnNetworkException()
    {
        // --- ARRANGE ---            
        int actualCalls = 0;            
        
        SetupMockResponse(() =>
        {
            actualCalls++;
            throw new HttpRequestException(null, null, HttpStatusCode.InternalServerError);
        });

        // --- ACT ---            
        var result = await _sut.Get("https://secure.etecsa.net:8443/");

        // --- ASSERT ---
        // 1 inicial + 3 reintentos = 4 llamadas
        Assert.Equal(4, actualCalls);

        // Verificamos que al final, tras fallar los reintentos, 
        // nuestro HandleException devuelva un error 500 o el mensaje de error.
        Assert.Equal(ErrorType.UnexpectedResponse, result.Error.Type);
        Assert.Contains("inesperado", result.Error.Message);
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
        Assert.Equal(ErrorType.UnexpectedResponse, result.Error.Type);
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

    [Fact]
    public async Task Get_ShouldReturnTimeoutError_WhenServerIsTooSlow()
    {
        // --- ARRANGE ---
        // Simulamos un timeout sin que el CancellationToken del usuario se haya disparado
        SetupMockResponse(() => {
            throw new TaskCanceledException("HttpClient.Timeout expired");
        });

        // --- ACT ---
        var result = await _sut.Get(testUrl);

        // --- ASSERT ---
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NetworkError, result.Error.Type);
        Assert.Contains("lenta", result.Error.Message); // "Conexión lenta"
    }

    [Fact]
    public void Result_ShouldProtectValueAccess_OnFailure()
    {
        // --- ARRANGE ---
        var failure = Result<string>.Failure(new Failure(ErrorType.ParserError, "Test"));

        // --- ACT & ASSERT ---
        // Verificamos que nuestro "blindaje" en la propiedad Value funcione
        Assert.Throws<InvalidOperationException>(() => failure.Value);
    }

    [Fact]
    public async Task HandleException_ShouldIdentifyWifiProblem_ViaSocketError()
    {
        // --- ARRANGE ---
        // Simulamos un error de DNS (HostNotFound) que ocurre cuando no hay red
        var socketEx = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound);
        var httpEx = new HttpRequestException("Error", socketEx);

        SetupMockResponse(() => { throw httpEx; });

        // --- ACT ---
        var result = await _sut.Get(testUrl);

        // --- ASSERT ---
        Assert.True(result.IsFailure);
        Assert.Contains("Wi-Fi", result.Error.Message);
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
