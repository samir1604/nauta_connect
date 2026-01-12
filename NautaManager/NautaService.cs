using ConnectionManager;
using ConnectionManager.Contracts;
using HtmlAgilityPack;
using NautaManager.Contracts;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NautaManager;

public class NautaService : INautaService
{
    private readonly IHttpConnection _connection;
    private Dictionary<string, string> _sessionFields = [];

    public event Action<string>? OnStatusMessageChanged;
    public event Action<string>? OnStatusChanged;
    public event Action<string>? OnErrorOccurred;
    public event Action<TimeSpan>?   OnTimeRemainingUpdated;
    public event Action<bool>? OnConnectionStateChanged;

    private const string csrfhw = "CSRFHW";

    //private static readonly string[] _fieldsToFind = ["CSRFHW", "wlanuserip", "loggerId"];
    //private const string _baseUrl = "https://secure.etecsa.net:8443/";        

    public NautaService(IHttpConnection connection)
    {
        _connection = connection;        
        _connection.OnRetryOccurred += _connection_OnRetryOccurred;
    }   

    public async Task<bool> IsPortalAvailableAsync(CancellationToken ct = default)
    {
        var response = await _connection.Get("/", null, ct);
        if (response.Status == 200 && response.FormFields.ContainsKey("CSRFHW"))
        {
            _sessionFields = response.FormFields;
            return true;
        }

        return false;
    }

    public async Task<bool> LoginAsync(
        string username, string password, CancellationToken ct = default)
    {
        OnStatusChanged?.Invoke("Iniciando sesión...");

        if (_sessionFields.Count == 0)
        {
            var isAvaliable = await IsPortalAvailableAsync(ct);
            if(!isAvaliable)
            {
                OnErrorOccurred?.Invoke("No se pudo conectar con el portal de ETECSA.");                
                return false;
            }            
        }
        
        var loginData = new Dictionary<string, string>(_sessionFields);
        loginData["username"] = username;
        loginData["password"] = password;
        
        var response = await _connection.Post("/LoginServlet", loginData, null, ct);

        if (response.Status == 302 && response.UrlRedirect.Contains("online.do"))
        {
            _sessionFields = response.FormFields;
            _sessionFields[csrfhw] = ExtractCsrfFromUrl(response.UrlRedirect) ?? _sessionFields[csrfhw];
            _sessionFields["ATTRIBUTE_UUID"] = ExtractUuidFromText(response.Response);

            OnStatusChanged?.Invoke("¡Conectado!");
            OnConnectionStateChanged?.Invoke(true);
            return true;
        }

        var alertError = ExtractAlertMessage(response.Response);
        OnErrorOccurred?.Invoke(alertError ?? "No se pudo completar el inicio de sesión. Inténtelo de nuevo.") ;        
        return false;
    }

    private string? ExtractAlertMessage(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        var scriptNodes = doc.DocumentNode.SelectNodes("//script");

        if (scriptNodes != null)
        {
            foreach (var node in scriptNodes)
            {
                string scriptContent = node.InnerText;
                var match = Regex.Match(scriptContent, @"alert\s*\(\s*['""](?<message>.*?)['""]\s*\)", RegexOptions.Singleline);

                if (match.Success) return match.Groups["message"].Value;
                
            }
        }

        return null; // No se encontró ningún alert real en código JS
    }

    private string? ExtractCsrfFromUrl(string url)
    {
        var match = Regex.Match(url, @"CSRFHW=([^&]*)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string ExtractUuidFromText(string html)
    {
        var match = Regex.Match(html, @"ATTRIBUTE_UUID=([A-Z0-9]+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private void _connection_OnRetryOccurred(
        string message, 
        int statusCode, 
        int retryCount, 
        int retryLeft, 
        TimeSpan delay)
    {
        OnStatusMessageChanged?.Invoke($"[Reintento {retryLeft}]/{retryCount}");
    }

    public async Task<bool> IsConnectionAvailableAsync(CancellationToken ct =default)
    {
        OnStatusMessageChanged?.Invoke("Verificando acceso al portal...");
        var response = await _connection.Get("/", null, ct);
        if (response.Status == 200 && response.FormFields.ContainsKey(csrfhw))
        {
            OnStatusMessageChanged?.Invoke("Portal detectado correctamente.");
            return true;
        }
        OnStatusMessageChanged?.Invoke("No se detectó el portal de ETECSA.");
        return false;
    }

    public async Task<Dictionary<string, string>> GetInitialData() 
    {
        throw new NotImplementedException();
    }

    public async Task<bool>  Login(string user, string pass, Dictionary<string, string> initialData)
    {
        /*
         * var formData = new Dictionary<string, string>
            {
                { "wlanuserip", initialData["wlanuserip"] },
                { "wlanacname", "" },
                { "wlanmac", "" },
                { "firsturl", "notFound.jsp" },
                { "ssid", "" },
                { "usertype", "" },
                { "gotopage", "/nauta_etecsa/LoginURL/pc_login.jsp" },
                { "successpage", "/nauta_etecsa/OnlineURL/pc_index.jsp" },
                { "loggerId", initialData["loggerId"] },
                { "lang", "es_ES" },
                { "username", user },
                { "password", pass },
                { "CSRFHW", initialData["CSRFHW"] }
            };
         */
        throw new NotImplementedException();
    }

    public void GetRemainingTime()
    {

    }

    public async Task Logout(string username, Dictionary<string, string> sessionData)
    {
        throw new NotImplementedException();
    }

    

    

    public Task LogoutAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task UpdateRemainingTimeAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
