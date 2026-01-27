using System.Runtime.Versioning;

namespace NautaCredential.Contracts;

[SupportedOSPlatform("windows")]
public abstract class CredentialManagerBase<T> : ICredentialManager<T>
{
    private readonly string _filePath;        

    protected CredentialManagerBase(string filename)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "NautaConnect");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, filename);
    }

    public abstract void Save(T credentials);
    public abstract T? Load();

    protected void SaveData(byte[] encryptedData) =>    
        File.WriteAllBytes(_filePath, encryptedData);

    protected byte[]? LoadData() =>
        File.Exists(_filePath) ? File.ReadAllBytes(_filePath) : null;

    public void Clear() {
        if(File.Exists(_filePath)) File.Delete(_filePath);        
    } 
}
