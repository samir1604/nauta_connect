using ConnectionManager.Contracts;
using ConnectionManager.DTO;
using ConnectionManager.Result;
using Moq;
using Moq.Language.Flow;
using Moq.Protected;
using NautaManager;
using NautaManager.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NautaConnect.Tests.NautaManager.Tests;

public class NautaServiceTests
{
    private readonly Mock<IHttpConnection> _connectionMock;    
    private readonly NautaService _sut;

    public NautaServiceTests()
    {
        _connectionMock = new Mock<IHttpConnection>();        
        _sut = new NautaService(
            _connectionMock.Object, new NautaDataParser());
    }

    [Fact]
    public async Task IsPortalAvailableAsync_ShouldReturnTrue_AndPopulateFields_WhenHtmlIsValid()
    {
        // Arrange
        var html = @"
        <form>
            <input type='hidden' name='CSRFHW' value='9a1128443c0b5766adb44cbaabca2a40' />
            <input type='hidden' name='wlanuserip' value='10.227.23.159' />
            <input type='hidden' name='loggerId' value='20260113145204186' />
        </form>";

        SetupMockGetResponse(() => Result.Success(new HttpResponse 
        {   
            RawContent = html 
        }));

        // Act
        var result = await _sut.IsPortalAvailableAsync();

        // Assert
        Assert.True(result);        
    }

    [Theory]
    [InlineData("saldo")]
    [InlineData("incorrecta")]
    public async Task LoginAsync_ShouldReturnFalse_AndShowAlertMessage_OnFailedLogin(
         string expectedAlert)
    {
        // Arrange                
        SetupMockGetResponse(() => Result.Success(new HttpResponse
        {            
            RawContent = "<input type='hidden' name='CSRFHW' value='123'/>"
        }));

        SetupMockPostResponse(() => Result<HttpResponse>.Failure(
            new Failure(ErrorType.InvalidCredentials, expectedAlert)));        

        string? capturedError = null;
        _sut.OnErrorOccurred += (msg) => capturedError = msg;

        // Act
        var result = await _sut.LoginAsync("user", "pass");

        // Assert
        Assert.False(result);        
        Assert.Equal(expectedAlert, capturedError);
    }    

    [Fact]
    public async Task LoginAsync_ShouldReturnTrue_AndUpdateSessionFields_OnSuccess()
    {
        // Arrange
        SetupMockGetResponse(() => Result.Success(new HttpResponse
        {            
            RawContent = "<input type='hidden' name='CSRFHW' value='inicial'/>"
        }));

        // Simular respuesta desde online.do
        var successHtml = "var urlParam = 'ATTRIBUTE_UUID=UUID123&CSRFHW=token456&loggerId=log789';";
        SetupMockPostResponse(() => Result.Success(new HttpResponse
        {            
            RawContent = successHtml,
            UrlRedirect = "https://secure.etecsa.net:8443/online.do"
        }));        

        bool stateChanged = false;
        _sut.OnConnectionStateChanged += (state) => stateChanged = state;

        // Act
        var result = await _sut.LoginAsync("user", "pass");

        // Assert
        Assert.True(result);
        Assert.True(stateChanged);
    }

    [Fact]
    public async Task LoginAsync_ShouldTryToAvailablePortal_IfSessionFieldsAreEmpty()
    {
        // Arrange: El Get devuelve éxito para que el Login pueda continuar
        SetupMockGetResponse(() => Result.Success(new HttpResponse
        {            
            RawContent = "<input type='hidden' name='CSRFHW' value='123'/>"
        }));
        
        SetupMockPostResponse(() => Result.Success(new HttpResponse
        {
            RawContent = "online.do",
            UrlRedirect = "online.do"
        }));
        
        // Act
        await _sut.LoginAsync("user", "pass");

        // Assert: Verificamos que se llamó al GET para inicializar        
        _connectionMock.Verify(c => 
            c.Get(
                "/", 
                null, 
                It.IsAny<CancellationToken>()), 
                Times.Once);       
    }    

    [Fact]
    public async Task IsPortalAvailableAsync_ShouldPropagateCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Simulamos cancelación inmediata

        SetupMockGetResponse(() => Result.Success(new HttpResponse 
        {
            RawContent = "Cancelada"
        }), cts.Token);
        
        // Act
        var result = await _sut.IsPortalAvailableAsync(cts.Token);

        // Assert
        Assert.False(result); // El portal no está disponible si se canceló la petición
    }

    [Fact]
    public async Task UpdateRemainingTimeAsync_ShouldTriggerEvent_WhenResponseIsValid()
    {
        // --- ARRANGE ---
        PrePopulateSessionAsync(_sut);
        
        SetupMockGetResponse(() => Result.Success(new HttpResponse
        {            
            RawContent = "<input type='hidden' name='CSRFHW' value='123'/>"
        }));

        SetupMockPostResponse(()=> Result.Success(new HttpResponse
        {            
            RawContent = "ATTRIBUTE_UUID=UUID123&username=user@nauta.com.cu",
            UrlRedirect = "online.do"
        }), "/LoginServlet");
        
        // Mock de la respuesta del tiempo
        var expectedTime = new TimeSpan(38, 3, 59);

        SetupMockPostResponse(() => Result.Success(new HttpResponse
        {            
            RawContent = "38:03:59"
        }), "/EtecsaQueryServlet");
        
        TimeSpan capturedTime = TimeSpan.Zero;
        _sut.OnTimeRemainingUpdated += (time) => capturedTime = time;

        // --- ACT ---
        await _sut.LoginAsync("user@nauta.com.cu", "pass");
        await _sut.UpdateRemainingTimeAsync();

        // --- ASSERT ---
        Assert.Equal(expectedTime, capturedTime);
    }

    [Fact]
    public async Task UpdateRemainingTimeAsync_ShouldShowError_WhenServerReturnsErrorop()
    {
        // --- ARRANGE ---
        // Simulamos sesión activa
        PrePopulateSessionAsync(_sut);

        SetupMockPostResponse(() => Result.Success(new HttpResponse
        {            
            RawContent = ""
        }), "/EtecsaQueryServlet");        

        string? errorMessage = null;
        _sut.OnErrorOccurred += (msg) => errorMessage = msg;

        // --- ACT ---
        await _sut.UpdateRemainingTimeAsync();

        // --- ASSERT ---
        Assert.Contains("expirado", errorMessage);
    }

    [Fact]
    public async Task UpdateRemainingTimeAsync_ShouldAbort_WhenNoSessionExists()
    {
        // --- ARRANGE ---
        // No hacemos Login, _sessionFields está vacío

        // --- ACT ---
        await _sut.UpdateRemainingTimeAsync();

        // --- ASSERT ---
        // Verificamos que NUNCA se llamó al Post de EtecsaQueryServlet
        _connectionMock.Verify(c => 
            c.Post("/EtecsaQueryServlet", 
            It.IsAny<IRequestContent>(), 
            It.IsAny<Action<IRequestConfiguration>>(),
            It.IsAny<CancellationToken>()), 
        Times.Never);
    }

    [Fact]
    public async Task UpdateRemainingTimeAsync_ShouldSendExactlyTenFieldsInPayload()
    {
        // --- ARRANGE ---
        // Llenamos el diccionario con los datos de prueba
        PrePopulateSessionAsync(_sut);

        SetupMockPostResponse(() => Result.Success(new HttpResponse
        {
            RawContent = "01:00:00"
        }));

        // --- ACT ---
        await _sut.UpdateRemainingTimeAsync();

        // --- ASSERT ---
        _connectionMock.Verify(c => c.Post(
            It.Is<string>(url => url.Contains("EtecsaQueryServlet")),
            It.Is<IRequestContent>(content => ValidateContent(content)), // Validación personalizada
            It.IsAny<Action<IRequestConfiguration>>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_ShouldClearSession_WhenServerReturnsSuccess()
    {
        // --- ARRANGE ---        
        PrePopulateSessionAsync(_sut);
        
        SetupMockPostResponse(() => Result.Success(new HttpResponse
        {            
            RawContent = "SUCCESS" // Lo que esperamos ver según el JS
        }), "/LogoutServlet");

        bool stateChangedCalled = false;
        _sut.OnConnectionStateChanged += (state) => stateChangedCalled = !state;

        // --- ACT ---
        await _sut.LogoutAsync();

        // --- ASSERT ---
        // Verificamos que se envió el campo 'remove' con valor '1'        
        _connectionMock.Verify(c => c.Post(
            It.IsAny<string>(),
            It.Is<IRequestContent>(content =>
                ((Dictionary<string, string>)((FormContent)content).RawData).ContainsKey("remove") &&
                    ((Dictionary<string, string>)((FormContent)content).RawData)["remove"] == "1"),
            It.IsAny<Action<IRequestConfiguration>>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
        
        Assert.True(stateChangedCalled);
    }

    private IReturnsResult<IHttpConnection> SetupMockPostResponse(
        Func<Result<HttpResponse>> response, string? url = null)
    {
        if (url == null)
        {
            return _connectionMock.Setup(c => 
            c.Post(                
                It.IsAny<string>(),
                It.IsAny<IRequestContent>(),
                It.IsAny<Action<IRequestConfiguration>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        }

        return _connectionMock.Setup(c =>
            c.Post(
                It.Is<string>(url => url.Contains(url)),
                It.IsAny<IRequestContent>(),
                It.IsAny<Action<IRequestConfiguration>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private IReturnsResult<IHttpConnection> SetupMockGetResponse(
        Func<Result<HttpResponse>> response, CancellationToken ct = default)
    {        
        return _connectionMock.Setup(c =>
            c.Get(
                It.IsAny<string>(),
                null,
                ct))
            .ReturnsAsync(response);
    }

    // Método auxiliar para validar el contenido del FormContent
    private bool ValidateContent(IRequestContent content)
    {        
        if (content is not FormContent formContent) return false;

        var data = (Dictionary<string, string>)formContent.RawData;
     
        bool hasTenFields = data.Count == 10;
        bool hasNoPassword = !data.ContainsKey("password");
        bool hasCorrectOp = data.GetValueOrDefault("op") == "getLeftTime";
        bool hasUUID = !string.IsNullOrEmpty(data.GetValueOrDefault("ATTRIBUTE_UUID"));

        return hasTenFields && hasNoPassword && hasCorrectOp && hasUUID;
    }

    private static void PrePopulateSessionAsync(NautaService service)
    {
        var fields = new Dictionary<string, string>
    {
        { "ATTRIBUTE_UUID", "05FA146A15E71A007139E45DA9536074" },
        { "CSRFHW", "a3096b170885d963b96e89495159b42b" },
        { "wlanuserip", "10.227.23.159" },
        { "loggerId", "20260113185802454+user@nauta.com.cu" },
        { "username", "user@nauta.com.cu" }
    };

        // Usamos reflexión para acceder al campo privado '_sessionFields'
        var fieldInfo = typeof(NautaService)
                .GetField("_sessionFields",
                    System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);

        fieldInfo?.SetValue(service, fields);
    }
}
