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
    public class NautaConnect 
    {
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private static readonly string[] _fieldsToFind = ["CSRFHW", "wlanuserip", "loggerId"];
        private const string _baseUrl = "https://secure.etecsa.net:8443/";        

        public NautaConnect()
        {
            
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,                
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            _httpClient = new HttpClient(handler);
        }

        public async Task<Dictionary<string, string>> GetInitialData() 
        {
            var data = new Dictionary<string, string>();            

            try
            {
                Console.WriteLine("Conectando con el portal de Etecsa...");

                var response = await _httpClient.GetAsync(_baseUrl);
                response.EnsureSuccessStatusCode();

                string htmlContent = await response.Content.ReadAsStringAsync();
                
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);

                foreach (var field in _fieldsToFind)
                {
                    var node = htmlDoc.DocumentNode.SelectSingleNode($"//input[@name='{field}']");
                    if (node != null)
                    {
                        data.Add(field, node.GetAttributeValue("value", ""));
                    }
                }
                
                var cookies = _cookieContainer.GetCookies(new Uri(_baseUrl));

                foreach (Cookie cookie in cookies)
                {
                    if (cookie.Name == "JSESSIONID")
                    {
                        Console.WriteLine($"[OK] Sesión detectada: {cookie.Value}");
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] No se pudo obtener los datos iniciales: {ex.Message}");
                return data;
            }
        }

        public async Task<bool>  Login(string user, string pass, Dictionary<string, string> initialData)
        {
            try
            {                
                var formData = new Dictionary<string, string>
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

                var requestContent = new FormUrlEncodedContent(formData);

                Console.WriteLine("Iniciando sesión...");

                var response = await _httpClient.PostAsync(_baseUrl + "LoginServlet", requestContent);

                Console.WriteLine("Status Code recibido: " + response.StatusCode);

                if (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Found)
                {
                    var redirectUrl = response?.Headers?.Location?.ToString();                    
                    
                    string newCsrf = ExtractCsrfFromUrl(redirectUrl);
                    initialData["CSRFHW"] = newCsrf; // Actualizamos para el Logout y consultas

                    Console.WriteLine("[OK] Login exitoso. Internet activado.");
                    return true;
                }

                Console.WriteLine("[Error] No se recibió la redirección esperada.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Error en Login: {ex.Message}");
                return false;
            }
        }

        public void GetRemainingTime()
        {

        }

        public async Task Logout(string username, Dictionary<string, string> sessionData)
        {
            try
            {
                var formData = new Dictionary<string, string>
                {
                    { "ATTRIBUTE_UUID", "9F2B43908ADF4D7E5D3AA5D4AA557C1C" },
                    { "CSRFHW", sessionData["CSRFHW"] },
                    { "wlanuserip", sessionData["wlanuserip"] },
                    { "loggerId", sessionData["loggerId"] + "+" + username },
                    { "username", username },
                    { "remove", "1" }
                };

                var content = new FormUrlEncodedContent(formData);

                Console.WriteLine("Cerrando sesión para evitar gastos...");
                var response = await _httpClient.PostAsync(_baseUrl + "LogoutServlet", content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[OK] Sesión cerrada correctamente. Ya puedes apagar o desconectar.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] No se pudo cerrar sesión: {ex.Message}");
            }
        }

        private string ExtractCsrfFromUrl(string? url)
        {
            if (String.IsNullOrEmpty(url)) return "";

            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return query["CSRFHW"] ?? "";
        }
    }
}
