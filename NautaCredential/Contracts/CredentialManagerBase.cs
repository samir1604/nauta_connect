using System.Runtime.Versioning;

namespace NautaCredential.Contracts;

[SupportedOSPlatform("windows")]
public abstract class CredentialManagerBase<T> : ICredentialManager<T>
{
    private readonly string _filePath;
    private const string FolderName = "NautaConnect";

    protected CredentialManagerBase(string filename)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, FolderName);

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);    
        }
        
        _filePath = Path.Combine(folder, filename);
    }

    public abstract void Save(T credentials);
    public abstract T? Load(string? username = null);
    public abstract void Delete(string username);
    public abstract void Clear();
    public abstract void SetDefault(string username);
    public abstract (string,  IReadOnlyCollection<string>) ListCredentials();

    protected void SaveFile(byte[] encryptedData) =>    
        File.WriteAllBytes(_filePath, encryptedData);

    protected byte[]? LoadFile() =>
        File.Exists(_filePath) ? File.ReadAllBytes(_filePath) : null;

    protected void ClearFile()
    {
        if (File.Exists(_filePath)) 
            File.Delete(_filePath);   
    }
}
