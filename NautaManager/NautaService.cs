using ConnectionManager.Contracts;
using ConnectionManager.DTO;
using HtmlAgilityPack;
using NautaManager.Contracts;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NautaManager;

public class NautaService : INautaService
{
    private readonly IHttpConnection _connection;
    private readonly IDataParser _dataParser;
    private Dictionary<string, string> _sessionFields = [];

    public event Action<string>? OnStatusMessageChanged;
    public event Action<string>? OnStatusChanged;
    public event Action<string>? OnErrorOccurred;
    public event Action<TimeSpan>?   OnTimeRemainingUpdated;
    public event Action<bool>? OnConnectionStateChanged;

    public NautaService(
        IHttpConnection connection, IDataParser dataParser)
    {
        _connection = connection;   
        _dataParser = dataParser;
        _connection.OnRetryOccurred += Connection_OnRetryOccurred;
    }   

    public async Task<bool> IsPortalAvailableAsync(CancellationToken ct = default)
    {
        _sessionFields.Clear();

        ShowStatusMessage("Verificando acceso al portal de ETECSA");

        var response = await _connection.Get(url:"/", ct: ct);

        if(response.IsSuccess && 
            !string.IsNullOrEmpty(response.RawContent))        
            _sessionFields = _dataParser.ParseSessionFieldFromForm(response.RawContent);            

        if (_sessionFields.ContainsKey(NautaServiceKeys.CSRFHWKey)) return true;
        
        ShowStatusMessage("No se detecto el portal de ETECSA");
        return false;
    }    

    public async Task<bool> LoginAsync(
        string username, string password, CancellationToken ct = default)
    {   
        if (_sessionFields.Count == 0 && !await IsPortalAvailableAsync(ct)) return false;
        
        var loginData = CreateLoginDataRequest(username, password, _sessionFields);

        ShowStatusMessage("Iniciando sesión...");

        var response = await _connection.Post(
            "/LoginServlet",
            new FormContent(loginData),
            cfg =>
            {                
                cfg.SetReferer("https://secure.etecsa.net:8443/");
                cfg.AddHeader("Origin", "https://secure.etecsa.net:8443");
            }, ct);

        if (response.IsSuccess && response.UrlRedirect.Contains("online.do")) 
        {
            _sessionFields[NautaServiceKeys.USERNAMEKey] = username;
            UpdateSessionFromJavascript(response.RawContent);
            ShowStatusMessage("Conectado!");
            ChangeConnectionState(true);
            return true;
        }

        var alertError = _dataParser.ExtractAlertMessageFromJs(
                response.RawContent) 
                    ?? "Credenciales incorrectas";

        ShowErrorMessage(alertError);
        return false;
    }

    public async Task UpdateRemainingTimeAsync(CancellationToken ct = default)
    {        
        if (!_sessionFields.ContainsKey(NautaServiceKeys.ATTRIBUTE_UUIDKey) || 
            !_sessionFields.ContainsKey(NautaServiceKeys.USERNAMEKey))
        {
            ShowErrorMessage("No hay una sesión activa para consultar el tiempo.");
            return;
        }

        var queryData = CreateRemainingTimeDataRequest(_sessionFields);
        
        var response = await _connection.Post(
            url: "/EtecsaQueryServlet",
            data: new FormContent(queryData),
            config: cfg =>
            {                
                string currentCsrf = _sessionFields.GetValueOrDefault(NautaServiceKeys.CSRFHWKey, "");
                cfg.SetReferer($"https://secure.etecsa.net:8443/web/online.do?CSRFHW={currentCsrf}&");
                cfg.AddHeader("Origin", "https://secure.etecsa.net:8443");
                cfg.AddHeader("Accept", "*/*");
            }, ct);
        
        if (response.IsSuccess && 
            ProcessRemainginTimeResponse(response.RawContent) is TimeSpan remaining)
        {   
                ShowRemaining(remaining);
        } else
        {
            _sessionFields.Clear();
            ShowErrorMessage("La sesión ha expirado por inactividad.");
            ChangeConnectionState(false);
        }
    }

    private TimeSpan? ProcessRemainginTimeResponse(string? rawContent)
    {
        if(rawContent != null &&
            _dataParser.TryParseConnectionTime(
                rawContent.Trim(), out TimeSpan remaining)) {
            return remaining; 
        }
        
        return null;        
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (!_sessionFields.ContainsKey(NautaServiceKeys.ATTRIBUTE_UUIDKey) &&
            !_sessionFields.ContainsKey(NautaServiceKeys.USERNAMEKey))
        {
            return;
        }

        ShowStatusMessage("Cerrando sesión...");
        
        var logoutData = CreateLogoOutDataRequest(_sessionFields);
                
        var response = await _connection.Post(
            url: "/LogoutServlet",
            data: new FormContent(logoutData),
            config: cfg =>
            {
                string currentCsrf = _sessionFields.GetValueOrDefault(NautaServiceKeys.CSRFHWKey, "");
                cfg.SetReferer($"https://secure.etecsa.net:8443/web/online.do?CSRFHW={currentCsrf}&");
                cfg.AddHeader("Origin", "https://secure.etecsa.net:8443");
            }, ct);
        
        if (response.IsSuccess && (response.RawContent?.Contains("SUCCESS") ?? false))
        {
            _sessionFields.Clear();
            ShowStatusMessage("Sesión cerrada.");
            ChangeConnectionState(false);
        }
        else
        {
            ShowErrorMessage("Error al cerrar sesión. Intente de nuevo.");
        }
    }

    private static Dictionary<string, string> CreateLogoOutDataRequest(
        Dictionary<string, string> actualSessionsFields) => new()
        {
            { NautaServiceKeys.ATTRIBUTE_UUIDKey, actualSessionsFields.GetValueOrDefault(NautaServiceKeys.ATTRIBUTE_UUIDKey, "")},
            { NautaServiceKeys.CSRFHWKey, actualSessionsFields.GetValueOrDefault(NautaServiceKeys.CSRFHWKey, "") },
            { NautaServiceKeys.WLANUSERIPKey, actualSessionsFields.GetValueOrDefault(NautaServiceKeys.WLANUSERIPKey, "") },
            { NautaServiceKeys.LOGGERIDKey, actualSessionsFields.GetValueOrDefault(NautaServiceKeys.LOGGERIDKey, "") },
            { NautaServiceKeys.USERNAMEKey, actualSessionsFields.GetValueOrDefault(NautaServiceKeys.USERNAMEKey, "") },            
            { NautaServiceKeys.SSIDKey, "" },
            { NautaServiceKeys.DOMAINKey, "" },
            { NautaServiceKeys.WLANACNAMEKey, "" },
            { NautaServiceKeys.WLANMACKey, "" },
            { "remove", "1" },
        };
    

    private static Dictionary<string, string> CreateRemainingTimeDataRequest(
        Dictionary<string, string> actualSessionFields) => new()
        {
            { NautaServiceKeys.OPKey, "getLeftTime" },
            { NautaServiceKeys.ATTRIBUTE_UUIDKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.ATTRIBUTE_UUIDKey, "") },
            { NautaServiceKeys.CSRFHWKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.CSRFHWKey, "") },
            { NautaServiceKeys.WLANUSERIPKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.WLANUSERIPKey, "") },
            { NautaServiceKeys.LOGGERIDKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.LOGGERIDKey, "") },
            { NautaServiceKeys.USERNAMEKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.USERNAMEKey, "") },
            { NautaServiceKeys.SSIDKey, "" },
            { NautaServiceKeys.DOMAINKey, "" },
            { NautaServiceKeys.WLANACNAMEKey, "" },
            { NautaServiceKeys.WLANMACKey, "" }
        };

    private static Dictionary<string, string> CreateLoginDataRequest(
        string user, 
        string password, 
        Dictionary<string, string> actualSessionFields) => new()
        {
            { NautaServiceKeys.WLANUSERIPKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.WLANUSERIPKey, "") },
            { NautaServiceKeys.WLANACNAMEKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.WLANACNAMEKey, "") },
            { NautaServiceKeys.WLANMACKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.WLANMACKey, "") },
            { NautaServiceKeys.FIRSTURLKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.FIRSTURLKey, "") },
            { NautaServiceKeys.SSIDKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.SSIDKey, "") },
            { NautaServiceKeys.USERTYPEKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.USERTYPEKey, "") },
            { NautaServiceKeys.GOTOPAGEKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.GOTOPAGEKey, "") },
            { NautaServiceKeys.SUCCESSPAGEKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.SUCCESSPAGEKey, "") },
            { NautaServiceKeys.LOGGERIDKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.LOGGERIDKey, "") },
            { NautaServiceKeys.LANGKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.LANGKey, "") },
            { NautaServiceKeys.CSRFHWKey, actualSessionFields.GetValueOrDefault(NautaServiceKeys.CSRFHWKey, "") },
            { NautaServiceKeys.USERNAMEKey, user },
            { NautaServiceKeys.PASSWORDKey, password },
        };
    

    private void UpdateSessionFromJavascript(string rawContent)
    {    
        var fields = _dataParser.ParseSessionFieldFromJs(rawContent);

        foreach (var (clave, valor) in fields)        
            _sessionFields[clave] = valor;
    }    

    private void ShowStatusMessage(string message) =>
        OnStatusMessageChanged?.Invoke(message);
    private void ChangeConnectionState(bool state) => 
        OnConnectionStateChanged?.Invoke(state);

    private void ShowErrorMessage(string msg) =>
        OnErrorOccurred?.Invoke(msg);          

    private void ShowRemaining(TimeSpan remaining) =>
        OnTimeRemainingUpdated?.Invoke(remaining);

    private void Connection_OnRetryOccurred(
        string message, 
        int statusCode, 
        int retryCount, 
        int retryLeft, 
        TimeSpan delay)
    {
        OnStatusMessageChanged?.Invoke($"[Reintento {retryLeft}]/{retryCount}");
    } 
}
