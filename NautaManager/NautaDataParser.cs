using ConnectionManager.Result;
using HtmlAgilityPack;
using NautaManager.Contracts;
using System.Text.RegularExpressions;

namespace NautaManager;

public partial class NautaDataParser : IDataParser
{
    [GeneratedRegex(@"alert\s*\(\s*['""](?<message>.*?)['""]\s*\)", RegexOptions.Singleline)]
    private static partial Regex AlertRegex();

    [GeneratedRegex(@"urlParam\s*=\s*[""']([^""']+)[""']", RegexOptions.Singleline)]
    private static partial Regex UrlParamExtractorRegex();

    [GeneratedRegex(@"[""']\s*\+\s*[""']", RegexOptions.Compiled)]
    private static partial Regex StringConcatenationRegex();

    public Result<Dictionary<string, string>> ParseSessionFieldFromForm(string html)
    {
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        var nodes = htmlDoc.DocumentNode.SelectNodes($"//input[@type='hidden' and @name]");
        if (nodes == null)
            return ServiceFailures.ParseError<Dictionary<string, string>>(
                "Datos insuficientes para establecer la conexión");

        foreach (var node in nodes)
        {
            string name = node.GetAttributeValue("name", "");
            string value = node.GetAttributeValue("value", "");
            if (!string.IsNullOrEmpty(name))
                fields.Add(name, value);
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
        var csrfhw = ExtractValueFromJs(html, NautaServiceKeys.CSRFHWKey);

        if (string.IsNullOrEmpty(attributeUUID) || string.IsNullOrEmpty(csrfhw))
            return ServiceFailures.ParseError<Dictionary<string, string>>(
                "No se pudo extraer la informacion");

        fields[NautaServiceKeys.ATTRIBUTE_UUIDKey] = attributeUUID;
        fields[NautaServiceKeys.CSRFHWKey] = csrfhw;        

        var loggerId = ExtractValueFromJs(html, NautaServiceKeys.LOGGERIDKey);
        var username = ExtractValueFromJs(html, NautaServiceKeys.USERNAMEKey);
            
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
        // Sanitizar HTML: eliminar concatenaciones de strings y caracteres de formato
        string sanitizedHtml = SanitizeJavaScript(html);

        // Extraer el contenido completo de urlParam
        var urlParamMatch = UrlParamExtractorRegex().Match(sanitizedHtml);

        if (!urlParamMatch.Success)
            return null;

        string urlParamContent = urlParamMatch.Groups[1].Value;

        // Buscar key=value dentro del contenido usando query string format
        return ExtractQueryStringValue(urlParamContent, key);
    }

    private static string SanitizeJavaScript(string html)
    {
        // Eliminar concatenaciones de strings JavaScript (" + " y ' + ')
        string sanitized = StringConcatenationRegex().Replace(html, "");

        // Eliminar caracteres de control
        sanitized = sanitized.Replace("\r", "")
                             .Replace("\n", "")
                             .Replace("\t", "");

        return sanitized;
    }

    private static string? ExtractQueryStringValue(string queryString, string key)
    {
        // Parsear manualmente el query string (más eficiente que regex para este caso)
        var pairs = queryString.Split('&');

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=', 2); // Limitar a 2 partes en caso de = en el valor

            if (keyValue.Length == 2 &&
                keyValue[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                string value = keyValue[1].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }
}
