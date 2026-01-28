using ConnectionManager.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace ConnectionManager.Configuration;

public static class ConnectionManagerConfigure
{
    public static IServiceCollection AddHttpClientConfig(
       this IServiceCollection services, CookieContainer cookieContainer)
    {
        services.AddSingleton(cookieContainer);

        services.AddHttpClient<IHttpConnection, HttpConnection>(client =>
        {
            client.BaseAddress = new Uri("https://secure.etecsa.net:8443/");
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            CookieContainer = cookieContainer,
            //Evitar que la conexión se "pudra" o quede obsoleta
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            //No dejar basura abierta si no se está usando la app
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            //Mantener el "túnel" abierto aunque no estés haciendo nada.
            KeepAlivePingDelay = TimeSpan.FromSeconds(15),
            //Saber rápido si el WiFi se desconectó de verdad.
            KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (m, c, ch, e) => true
            }
        });

        return services;
    }
}
