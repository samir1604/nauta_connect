using ConnectionManager.Contracts;

namespace ConnectionManager.Internal;

internal class RequestConfigurationBuilder(
    HttpRequestMessage _request) : IRequestConfiguration
{
    public void AddHeader(string name, string value) =>
        _request.Headers.TryAddWithoutValidation(name, value);

    public void SetReferer(string url) => 
        _request.Headers.Referrer = new Uri(url);    

    public void SetUserAgent(string userAgent) =>
        _request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
}
