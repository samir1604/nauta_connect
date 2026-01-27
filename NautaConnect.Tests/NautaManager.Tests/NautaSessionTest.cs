using Moq;
using NautaManager;
using NautaManager.Contracts;
using NautaManager.Models;
using NautaManager.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NautaConnect.Tests.NautaManager.Tests;

public class NautaSessionTest : IDisposable
{
    private readonly SessionManager _sut = new();
    private readonly SessionInfo _testSession;

    public NautaSessionTest()
    {
        _sut = new SessionManager();
        _testSession = new SessionInfo
        {
            Username = "test_user@nauta.com.cu",
            AttributeUUID = "abc-123-uuid",
            CsrfHw = "csrf-token-789",
            LoggerId = "logger-456",
            LoginTime = DateTime.Now
        };
    }

    [Fact]
    public async Task SaveSession_ShouldCreatePhysicalFile()
    {
        // Arrange
        var session = new SessionInfo 
        { 
            Username = "test@nauta.com.cu", 
            AttributeUUID = "123" 
        };

        // Act
        var result = await _sut.SaveSession(session);

        // Assert
        Assert.True(result.IsSuccess);
        var restored = await _sut.GetActiveSession();        
        var actualUsername = restored.Value?.Username;
        Assert.Equal(session.Username, actualUsername);
    }

    [Fact]
    public async Task SaveSession_ShouldReturnSuccess_WhenDataIsValid()
    {
        // Act
        var result = await _sut.SaveSession(_testSession);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetActiveSession_ShouldReturnSavedData_AfterSaving()
    {
        // Arrange
        await _sut.SaveSession(_testSession);

        // Act
        var result = await _sut.GetActiveSession();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(_testSession.Username, result.Value?.Username);
        Assert.Equal(_testSession.AttributeUUID, result.Value?.AttributeUUID);
    }

    [Fact]
    public async Task GetActiveSession_ShouldReturnNull_WhenFileDoesNotExist()
    {
        // Arrange
        _sut.DeleteSession();

        // Act
        var result = await _sut.GetActiveSession();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task DeleteSession_ShouldRemoveFileFromDisk()
    {
        // Arrange
        await _sut.SaveSession(_testSession);

        // Act
        _sut.DeleteSession();
        var result = await _sut.GetActiveSession();

        // Assert
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetActiveSession_ShouldHandleCorruptJson_ByDeletingIt()
    {
        // Arrange: Escribir basura en el archivo de sesión manualmente
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "NautaConnect");
        var path = Path.Combine(folder, "active_session.json");
        File.WriteAllText(path, "{ este no es un json valido }");

        // Act
        var result = await _sut.GetActiveSession();

        // Assert
        Assert.False(result.IsSuccess); // Debe fallar el parseo
        Assert.False(File.Exists(path)); // El manager debe haber borrado el archivo corrupto
    }


    public void Dispose()
    {
        _sut.DeleteSession();
    }
}
