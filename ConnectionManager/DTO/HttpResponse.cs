namespace ConnectionManager.DTO;

public record HttpResponse
{   
    public string RawContent { get; set; } = string.Empty;
    public string UrlRedirect { get; set; } = string.Empty;    
    public Dictionary<string, string[]> Headers { get; set; } = [];
    public Dictionary<string, string> Cookies { get; set; } = [];    
}
