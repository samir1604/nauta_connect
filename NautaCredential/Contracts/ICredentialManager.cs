namespace NautaCredential.Contracts;

public interface ICredentialManager<T>
{
    void Save(T credentials);
    T? Load(string? username = null);
    void Delete(string username);
    void Clear();
    void SetDefault(string username);
    (string,  IReadOnlyCollection<string>) ListCredentials();
}
