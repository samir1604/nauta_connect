namespace NautaCredential.Contracts;

public interface ICredentialManager<T>
{
    void Save(T credentials);
    T? Load();
    void Clear();
}
