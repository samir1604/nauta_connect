using ConnectionManager;
using HtmlAgilityPack;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NautaManager
{
    public class NautaService 
    {
        private readonly NautaConnection _connection;
        public event Action<string>? OnStatusMessageChanged;

        private const string csrfhw = "CSRFHW";

        private static readonly string[] _fieldsToFind = ["CSRFHW", "wlanuserip", "loggerId"];
        private const string _baseUrl = "https://secure.etecsa.net:8443/";        

        public NautaService(NautaConnection connection)
        {
            _connection = connection;
            _connection.OnRetryOccurred += _connection_OnRetryOccurred;
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
    }
}
