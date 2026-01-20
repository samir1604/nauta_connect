using ConnectionManager.Contracts;
using ConnectionManager.DTO;
using NautaManager.Contracts;
using ConnectionManager.Result;

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

        return await _connection.Get(url: "/", ct: ct)
            .BindAsync(response => _dataParser.ParseSessionFieldFromForm(response.RawContent))
            .BindAsync(fields =>
            {
                if (fields.ContainsKey(NautaServiceKeys.CSRFHWKey))
                    return Result.Success(fields);

                return Result<Dictionary<string, string>>.Failure(
                    new Failure(ErrorType.ParserError, "No se detecto el portal de ETECSA"));
            }).Fold(fields => 
            {
                _sessionFields = fields;
                return true;
            }, 
                failure => {
                ShowStatusMessage(failure.Message);
                return false;
            });
    }    

    public async Task<bool> LoginAsync(
        string username, string password, CancellationToken ct = default)
    {   
        if (_sessionFields.Count == 0 && !await IsPortalAvailableAsync(ct)) return false;

        var loginData = CreateLoginDataRequest(username, password, _sessionFields);

        ShowStatusMessage("Iniciando sesión...");

        return await _connection.Post(
            "/LoginServlet",
            new FormContent(loginData),
            cfg =>
            {
                cfg.SetReferer("https://secure.etecsa.net:8443/");
                cfg.AddHeader("Origin", "https://secure.etecsa.net:8443");
            }, ct)
            .BindAsync(EnsureLoginRedirect)
            .BindAsync(response => _dataParser.ParseSessionFieldFromJs(response.RawContent))
            .Fold(fields => {
                foreach (var field in fields) _sessionFields[field.Key] = field.Value;
                _sessionFields[NautaServiceKeys.USERNAMEKey] = username;
                ShowStatusMessage("Conectado!");
                ChangeConnectionState(true);
                return true;
            }, failure => {
                ShowErrorMessage(failure.Message);
                return false;
            });        
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
        
        await _connection.Post(
            url: "/EtecsaQueryServlet",
            data: new FormContent(queryData),
            config: cfg =>
            {                
                string currentCsrf = _sessionFields.GetValueOrDefault(NautaServiceKeys.CSRFHWKey, "");
                cfg.SetReferer($"https://secure.etecsa.net:8443/web/online.do?CSRFHW={currentCsrf}&");
                cfg.AddHeader("Origin", "https://secure.etecsa.net:8443");
                cfg.AddHeader("Accept", "*/*");
            }, ct)
            .BindAsync(response => ProcessRemainginTimeResponse(response.RawContent))
            .Fold(ShowRemaining, 
            failure => 
            {
                _sessionFields.Clear();                
                ChangeConnectionState(false);
                ShowErrorMessage("La sesión ha expirado por inactividad.");
            });
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
                
        await _connection.Post(
            url: "/LogoutServlet",
            data: new FormContent(logoutData),
            config: cfg =>
            {
                string currentCsrf = _sessionFields.GetValueOrDefault(NautaServiceKeys.CSRFHWKey, "");
                cfg.SetReferer($"https://secure.etecsa.net:8443/web/online.do?CSRFHW={currentCsrf}&");
                cfg.AddHeader("Origin", "https://secure.etecsa.net:8443");
            }, ct)
            .BindAsync(CheckCloseSessionResponse)
            .Fold(() =>
            {
                _sessionFields.Clear();
                ShowStatusMessage("Sesión cerrada.");
                ChangeConnectionState(false);
            }, failure => 
            {
                ShowErrorMessage(failure.Message);

            });
    }

    private static Result CheckCloseSessionResponse(HttpResponse response)
    {
        if (response.RawContent.Contains("SUCCESS"))
            return Result.Success();
        return Result.Failure(new Failure(
            ErrorType.UnexpectedResponse, 
            "Error al cerrar sesión. Intente de nuevo."));
    }

    private static Result<HttpResponse> EnsureLoginRedirect(HttpResponse response)
    {
        if (response.UrlRedirect.Contains("online.do"))
            return Result.Success(response);

        return Result<HttpResponse>.Failure(new Failure(
            ErrorType.InvalidCredentials,
            "No se pudo establecer la conexión. Verifique sus crendenciales"));
    }

    private Result<TimeSpan> ProcessRemainginTimeResponse(string? rawContent)
    {
        if (rawContent != null &&
            _dataParser.TryParseConnectionTime(
                rawContent.Trim(), out TimeSpan remaining))
            return Result.Success(remaining);

        return Result<TimeSpan>.Failure(new Failure(
            ErrorType.SessionExpired, "La sesión ha expirado por inactividad."));
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
