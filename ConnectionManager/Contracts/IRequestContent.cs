namespace ConnectionManager.Contracts;

public enum HttpContentType { Form, Json }
public interface IRequestContent
{
    HttpContentType Type { get; }
    object RawData { get; }
}
