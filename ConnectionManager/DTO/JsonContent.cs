using ConnectionManager.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager.DTO
{
    public class JsonContent(object data) : IRequestContent
    {
        public HttpContentType Type => HttpContentType.Json;

        public object RawData { get; } = data ?? throw new ArgumentNullException(nameof(data));
    }
}
