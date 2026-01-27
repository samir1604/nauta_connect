using ConnectionManager.Results;
using NautaManager.Models;

namespace NautaManager.Contracts;

public interface ISessionManager
{
    Task<Result> SaveSession(SessionInfo session);
    Task<Result<SessionInfo?>> GetActiveSession();
    void DeleteSession();
}
