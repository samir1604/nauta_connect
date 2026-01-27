using NautaCredential.Contracts;
using NautaCredential.DTO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NautaCredential;

[SupportedOSPlatform("windows")]
public class NautaCredentialManager : CredentialManagerBase<UserCredentials>
{
    private static readonly byte[] Entropy = "Nauta-Security-Salt-2026"u8.ToArray();

    public NautaCredentialManager() : base("nauta_user.dat")
    {
    }

    public override UserCredentials? Load()
    {
        try
        {
            var encripted = LoadData();
            if (encripted == null) return null;            

            var decryptedData = ProtectedData.Unprotect(
                encripted, Entropy, DataProtectionScope.CurrentUser);

            var json = Encoding.UTF8.GetString(decryptedData);
            return JsonSerializer.Deserialize<UserCredentials>(json);
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public override void Save(UserCredentials credentials)
    {
        var json = JsonSerializer.Serialize(credentials);
        var data = Encoding.UTF8.GetBytes(json);

        // Cifrado usando la cuenta de usuario actual de Windows
        var encryptedData = ProtectedData.Protect
            (data, Entropy, DataProtectionScope.CurrentUser);

        SaveData(encryptedData);        
    }
}
