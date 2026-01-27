using ConnectionManager.Results;
using NautaManager.Contracts;
using NautaManager.Failures;
using NautaManager.Models;
using System.Text.Json;

namespace NautaManager.Persistence;

public class SessionManager : ISessionManager
{
    private readonly string _filePath;

    public SessionManager()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), 
            "NautaConnect");

        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "active_session.json");
    }

    public async Task<Result> SaveSession(SessionInfo session)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                session, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
            return Result.Success();
        }
        catch (IOException ex)
        {
            return ServiceFailures.IOError(
                "Error al guardar sesión", ex.Message);
        }       
    }

    public async Task<Result<SessionInfo?>> GetActiveSession()
    {
        if (!File.Exists(_filePath))
            return Result<SessionInfo?>.Success(null);
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            var session = JsonSerializer.Deserialize<SessionInfo>(json);
            return Result.Success(session);
        }
        catch (Exception ex)
        {
            DeleteSession();
            return ServiceFailures.IOError<SessionInfo?>(
                    "Error leyendo el archivo de sessión", ex.Message);
            
        }
    }

    public void DeleteSession()
    {
        try
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }
        catch { }        
    }
}
