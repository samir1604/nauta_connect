using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NautaManager.Contracts;

public interface IDataParser
{
    Dictionary<string, string> ParseSessionFieldFromForm(string html);
    Dictionary<string, string> ParseSessionFieldFromJs(string html);
    string? ExtractAlertMessageFromJs(string html);
    bool TryParseConnectionTime(string timeStr, out TimeSpan interval);
}
