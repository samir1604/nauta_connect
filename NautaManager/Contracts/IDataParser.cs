using ConnectionManager.Result;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NautaManager.Contracts;

public interface IDataParser
{
    Result<Dictionary<string, string>> ParseSessionFieldFromForm(string html);
    Result<Dictionary<string, string>> ParseSessionFieldFromJs(string html);
    public Result IsDocumentCleanFromJsAlert(string html);
    bool TryParseConnectionTime(string timeStr, out TimeSpan interval);
}
