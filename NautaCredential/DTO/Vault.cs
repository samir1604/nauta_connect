namespace NautaCredential.DTO;

public class Vault
{
    public string? DefaultUsername { get; set; }
    public Dictionary<string, UserCredentials> Accounts = new();
}

