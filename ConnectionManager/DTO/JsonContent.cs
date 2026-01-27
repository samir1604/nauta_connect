using ConnectionManager.Contracts;

namespace ConnectionManager.DTO
{
    public class JsonContent(object data) : IRequestContent
    {
        public HttpContentType Type => HttpContentType.Json;

        public object RawData { get; } = data ?? throw new ArgumentNullException(nameof(data));
    }
}
