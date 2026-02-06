using NautaCredential.Contracts;
using NautaCredential.DTO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NautaCredential;

[SupportedOSPlatform("windows")]
public class NautaCredentialManager() : CredentialManagerBase<UserCredentials>("nauta_user.dat")
{
    private static readonly byte[] Entropy = "Nauta-Security-Salt-2026"u8.ToArray();

    public override UserCredentials? Load(string? username = null)
    {
        try
        {
            Vault? vault = LoadVault();
            if(vault == null) return null;
            string? targetUsername = username ?? vault.DefaultUsername;
            return targetUsername == null ? null : vault.Accounts.GetValueOrDefault(targetUsername);
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public override void Delete(string username)
    {
        Vault? vault = LoadVault();
        if (vault == null || !vault.Accounts.Remove(username)) return;

        if(vault.DefaultUsername == username) vault.DefaultUsername = null; 
        SaveVault(vault);
    }

    public override void Clear()
    {
        ClearFile();
    }

    public override void SetDefault(string username)
    {
        Vault? vault = LoadVault();
        if (vault == null || !vault.Accounts.ContainsKey(username)) return;
        
        vault.DefaultUsername = username;
        SaveVault(vault);
    }

    public override (string,  IReadOnlyCollection<string>) ListCredentials()
    {
        Vault? vault = LoadVault();
        if(vault == null) return (string.Empty, []);
        
        return (vault.DefaultUsername ?? string.Empty, vault.Accounts.Keys);
    }

    public override void Save(UserCredentials credentials)
    {
        Vault vault = LoadVault() ?? new Vault();
        vault.Accounts[credentials.Username] = credentials;
        
        if(string.IsNullOrEmpty(vault.DefaultUsername))
            vault.DefaultUsername = credentials.Username;
        
        SaveVault(vault);
    }
    
    private Vault? LoadVault()
    {
        try {
            byte[]? encrypted = LoadFile();
            if(encrypted == null) return null;
            
            byte[] decrypted = ProtectedData.Unprotect(
                encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Vault>(Encoding.UTF8.GetString(decrypted));
        } catch { return null; }
    }
    
    private void SaveVault(Vault vault)
    {
        string json = JsonSerializer.Serialize(vault);
        byte[] data = Encoding.UTF8.GetBytes(json);
        byte[] encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        SaveFile(encrypted);
    }
}
