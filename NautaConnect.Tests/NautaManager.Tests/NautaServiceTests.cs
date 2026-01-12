using ConnectionManager.Contracts;
using ConnectionManager.DTO;
using Moq;
using NautaManager;
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
        _sut = new NautaService(_connectionMock.Object);
    }

    [Fact]
    public async Task IsPortalAvailableAsync_ShouldReturnTrue_WhenCsrfIsPresent()
    {
        // ARRANGE
        // Simulamos que ETECSA responde con el campo necesario
        var fakeResponse = new HttpResponse
        {
            Status = 200,
            FormFields = new Dictionary<string, string> { { "CSRFHW", "valid_token" } }
        };

        _connectionMock
            .Setup(c => c.Get(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(fakeResponse);

        // ACT
        bool result = await _sut.IsPortalAvailableAsync();

        // ASSERT
        Assert.True(result);
    }

    [Fact]
    public async Task LoginAsync_ShouldUseDataFromInitialGet_AndReturnTrueOnSuccess()
    {
        // --- ARRANGE ---
        string username = "usuario@nauta.com.cu";
        string password = "password123";
        
        var portalResponse = new HttpResponse
        {
            Status = 200,
            FormFields = new Dictionary<string, string> {
            { "CSRFHW", "token_abc_123" },
            { "wlanuserip", "10.10.0.5" }
        }
        };
        
        var loginSuccessResponse = new HttpResponse
        {
            Status = 200,
            UrlRedirect = "https://secure.etecsa.net:8443/welcome.do",
            Response = "<html>Bienvenido</html>"
        };

        // Mock devuelve el Get al acceder al portal
        _connectionMock.SetupSequence(c => 
            c.Get(
                It.IsAny<string>(), 
                null, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(portalResponse);

        // Mock devuelve el resultado al hacer Login
        _connectionMock.Setup(c => 
            c.Post(
                It.IsAny<string>(), 
                It.IsAny<Dictionary<string, string>>(), 
                null, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginSuccessResponse);

        // --- ACT ---
        // Internamente, LoginAsync debería llamar a Get primero si no tiene los tokens
        bool result = await _sut.LoginAsync(username, password);

        // --- ASSERT ---
        Assert.True(result);
        // Verificamos que se llamó al POST con los datos combinados
        _connectionMock.Verify(c => c.Post(
            It.IsAny<string>(),
            It.Is<Dictionary<string, string>>(d => d["CSRFHW"] == "token_abc_123" && d["username"] == username),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnFalse_WhenHtmlContainsAlert()
    {
        // --- ARRANGE ---
        string? errorMessage = null;
        _sut.OnErrorOccurred += (msg) => errorMessage = msg;

        // HTML real que nos pasaste
        string etecsaErrorHtml = "<html>... <script type='text/javascript'>alert('Entre el nombre de usuario y contraseña correctos.');</script> ...</html>";

        /*
        var portalResponse = new HttpResponse
        {
            Status = 200,
            FormFields = new Dictionary<string, string> {
                { "CSRFHW", "token_abc_123" },
                { "wlanuserip", "10.10.0.5" }
            }
        };

        // Mock devuelve el Get al acceder al portal
        _connectionMock.SetupSequence(c =>
            c.Get(
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(portalResponse);
        */
        var errorResponse = new HttpResponse
        {
            Status = 200,
            Response = etecsaErrorHtml,
            UrlRedirect = "https://secure.etecsa.net:8443//LoginServlet"
        };

        _connectionMock.Setup(c => 
            c.Post(
                It.IsAny<string>(), 
                It.IsAny<Dictionary<string, string>>(),
                null,
                It.IsAny<CancellationToken>()))
                    .ReturnsAsync(errorResponse);

        // --- ACT ---
        var result = await _sut.LoginAsync("user", "pass");

        // --- ASSERT ---
        Assert.False(result);
        Assert.Contains("correctos", errorMessage);
    }

    [Fact]
    public async Task LoginAsync_ShouldSucceed_WhenResponseIs302WithCorrectLocation()
    {
        // ARRANGE
        string expectedRedirect = "https://secure.etecsa.net:8443/web/online.do?CSRFHW=588613689e8d5447ceebee8bd3099d35&";

        var successResponse = new HttpResponse
        {
            Status = 302,
            UrlRedirect = expectedRedirect // En nuestro DTO, UrlRedirect captura el Location final
        };

        _connectionMock.Setup(c => c.Post(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), null, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(successResponse);

        // ACT
        bool result = await _sut.LoginAsync("user", "pass");

        // ASSERT
        Assert.True(result);        
    }
}
