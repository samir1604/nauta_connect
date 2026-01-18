using ConnectionManager.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager.DTO;

public class FormContent(
    Dictionary<string, string> data) : IRequestContent
{
    public HttpContentType Type => HttpContentType.Form;

    public object RawData { get; } = data 
        ?? throw new ArgumentNullException(nameof(data));
}
