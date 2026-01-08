using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager.DTO;

public class HttpRequest
{
    public Dictionary<string, string> FormData { get; set; } = [];
}
