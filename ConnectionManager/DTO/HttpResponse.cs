using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager.DTO;

public class HttpResponse
{
    public int Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string UrlRedirect { get; set; } = string.Empty;
    public Dictionary<string, string> FormFields { get; set; } = [];
    public Dictionary<string, string[]> Headers { get; set; } = [];
    public Dictionary<string, string> Cookies { get; set; } = [];
    public bool IsSuccess => Status >= 200 && Status < 300;
}
