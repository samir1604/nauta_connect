using ConnectionManager.Result;
using HtmlAgilityPack;
using NautaManager.Contracts;
using System.Text.RegularExpressions;

namespace NautaManager;

public partial class NautaDataParser : IDataParser
{
    [GeneratedRegex(@"alert\s*\(\s*['""](?<message>.*?)['""]\s*\)", RegexOptions.Singleline)]
    private static partial Regex AlertRegex();

    [GeneratedRegex(@"(?<key>[^&""\s'=;]+)=(?<value>[^&""\s';]+)", RegexOptions.Multiline)]
    private static partial Regex SessionFieldsRegex();

    public Result<Dictionary<string, string>> ParseSessionFieldFromForm(string html)
    {
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        var nodes = htmlDoc.DocumentNode.SelectNodes($"//input[@type='hidden' and @name]");
        if (nodes == null)
            return ServiceFailures.ParseError<Dictionary<string, string>>(
                "Datos insuficientes para establecer la conexión");

        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                string name = node.GetAttributeValue("name", "");
                string value = node.GetAttributeValue("value", "");
                if (!string.IsNullOrEmpty(name))
                    fields.Add(name, value);
            }
        }

        return Result.Success(fields);
    }

    public Result<Dictionary<string, string>> ParseSessionFieldFromJs(string html)
    {
        return IsDocumentCleanFromJsAlert(html)
            .Bind(() => ExtractSessionFields(html));
    }

    private static Result<Dictionary<string, string>> ExtractSessionFields(string html)
    {
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);

        var attributeUUID = ExtractValueFromJs(html, NautaServiceKeys.ATTRIBUTE_UUIDKey);

        if (string.IsNullOrEmpty(attributeUUID))
            return ServiceFailures.ParseError<Dictionary<string, string>>(
                "No se pudo extraer la informacion");            

        var csrfhw = ExtractValueFromJs(html, NautaServiceKeys.CSRFHWKey);
        var loggerId = ExtractValueFromJs(html, NautaServiceKeys.LOGGERIDKey);
        var username = ExtractValueFromJs(html, NautaServiceKeys.USERNAMEKey);

        if (!string.IsNullOrEmpty(attributeUUID))
            fields[NautaServiceKeys.ATTRIBUTE_UUIDKey] = attributeUUID;
        if (!string.IsNullOrEmpty(csrfhw))
            fields[NautaServiceKeys.CSRFHWKey] = csrfhw;
        if (!string.IsNullOrEmpty(loggerId))
            fields[NautaServiceKeys.LOGGERIDKey] = loggerId;
        if (!string.IsNullOrEmpty(username))
            fields[NautaServiceKeys.USERNAMEKey] = username;

        return Result.Success(fields);
    }

    public Result IsDocumentCleanFromJsAlert(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var scriptNodes = doc.DocumentNode.SelectNodes("//script");

        if (scriptNodes != null)
        {
            foreach (var node in scriptNodes)
            {
                string scriptContent = node.InnerText;
                
                var match = AlertRegex().Match(scriptContent);

                if (match.Success)
                {
                    var alertContent = match.Groups["message"].Value;
                    var errorType = MapAlertToErrorType(alertContent);
                    return Result.Failure(new Failure(errorType, alertContent));
                }
            }
        }

        return Result.Success();
    }

    private static ErrorType MapAlertToErrorType(string content)
    {
        string lowerContent = content.ToLower();
        if (lowerContent.Contains("saldo")) return ErrorType.NoBalance;
        if (lowerContent.Contains("usuario") || lowerContent.Contains("contraseña"))
            return ErrorType.InvalidCredentials;

        return ErrorType.UnexpectedResponse;
    }   

    public bool TryParseConnectionTime(string timeStr, out TimeSpan interval)
    {
        interval = TimeSpan.Zero;

        var parts = timeStr?.Split(':');
        if (parts == null || parts.Length != 3) return false;

        if (long.TryParse(parts[0], out long hours) &&
            int.TryParse(parts[1], out int minutes) &&
            int.TryParse(parts[2], out int seconds))
        {
            interval = TimeSpan.FromHours(hours)
                     + TimeSpan.FromMinutes(minutes)
                     + TimeSpan.FromSeconds(seconds);

            return true;
        }

        return false;
    }

    private static string? ExtractValueFromJs(string html, string key)
    {
        var matches = SessionFieldsRegex().Matches(html);

        foreach (Match match in matches)
        {
            string foundKey = match.Groups["key"].Value.Trim();
            string foundValue = match.Groups["value"].Value.Trim();

            if (foundKey.Equals(key, StringComparison.OrdinalIgnoreCase))
            {                
                return string.IsNullOrWhiteSpace(foundValue) ? null : foundValue;
            }
        }
        return null;
    }
}
