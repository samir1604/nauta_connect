namespace NautaManager.Contracts
{
    public interface INautaService
    {
        event Action<string> OnStatusChanged;
        event Action<string> OnErrorOccurred;
        event Action<TimeSpan> OnTimeRemainingUpdated;
        event Action<bool> OnConnectionStateChanged;
        event Action<string>? OnStatusMessageChanged;

        Task<bool> IsPortalAvailableAsync(CancellationToken ct = default);
        Task<bool> LoginAsync(string username, string password, CancellationToken ct = default);
        Task LogoutAsync(CancellationToken ct = default);
        Task UpdateRemainingTimeAsync(CancellationToken ct = default);
        Task<bool> TryRestoreSessionAsync();
        Task<bool> LoginWithStoredCredentialsAsync(string? specificUser = null);
    }
}
