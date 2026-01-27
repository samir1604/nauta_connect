using ConnectionManager.Contracts;

namespace ConnectionManager.DTO;

public class FormContent(
    Dictionary<string, string> data) : IRequestContent
{
    public HttpContentType Type => HttpContentType.Form;

    public object RawData { get; } = data 
        ?? throw new ArgumentNullException(nameof(data));
}
