namespace NautaManager.Models;

public record SessionInfo()
{
    public string Username { get; init; } = string.Empty;
    public string AttributeUUID { get; init; } = string.Empty;
    public string CsrfHw { get; init; } = string.Empty;
    public string UserIP { get; init; } = string.Empty;
    public DateTime LoginTime { get; init; }
    public string LoggerId { get; init; } = string.Empty;
}
