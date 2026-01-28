// See https://aka.ms/new-console-template for more information
using CommandLine;
using ConnectionManager;
using ConnectionManager.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nauta.Cli.Handles;
using Nauta.Cli.LineOptions;
using NautaCredential;
using NautaCredential.Contracts;
using NautaCredential.DTO;
using NautaManager;
using NautaManager.Contracts;
using NautaManager.Parsers;
using NautaManager.Persistence;
using System.Net;

var builder = Host.CreateApplicationBuilder();

var cookieContainer = new CookieContainer();
builder.Services.AddSingleton(cookieContainer);
builder.Services.AddHttpClient<IHttpConnection, HttpConnection>(client =>
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
builder.Services.AddSingleton<INautaService, NautaService>();
builder.Services.AddSingleton<IDataParser, NautaDataParser>();
builder.Services.AddSingleton<ISessionManager, SessionManager>();
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<ICredentialManager<UserCredentials>, NautaCredentialManager>();
}

using IHost host = builder.Build();

var parserResult = Parser.Default.ParseArguments<Options>(args);

await Parser.Default.ParseArguments<Options>(args)
    .WithParsedAsync(async opts =>
    {
        if (!opts.Login && !opts.Logout && !opts.Status)
        {
            var helpText = CommandLine.Text.HelpText.AutoBuild(parserResult, h => h, e => e);
            Console.WriteLine(helpText);
            return;
        }
        var nautaService = host.Services.GetRequiredService<NautaService>();
        var credentialManager = host.Services.GetRequiredService<ICredentialManager<UserCredentials>>();

        SetupConsoleEvents(nautaService);

        var handler = new CommandHandler(nautaService, credentialManager);
        await handler.ExecuteOptionsAsync(opts);
    });

static void SetupConsoleEvents(INautaService nautaService)
{
    nautaService.OnStatusMessageChanged += (msg) =>
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[*] {msg}");
        Console.ResetColor();
    };

    nautaService.OnErrorOccurred += (msg) =>
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[!] ERROR: {msg}");
        Console.ResetColor();
    };

    nautaService.OnConnectionStateChanged += (isConnected) =>
    {
        if (isConnected)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[+] Estado: En línea");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[-] Estado: Desconectado");
        }
        Console.ResetColor();
    };

    nautaService.OnTimeRemainingUpdated += (time) =>
    {
        Console.Write("Tiempo restante: ");

        // Colores dinámicos según el tiempo
        if (time.TotalMinutes < 10) Console.ForegroundColor = ConsoleColor.Red;
        else if (time.TotalHours < 1) Console.ForegroundColor = ConsoleColor.Yellow;
        else Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine($"{time:hh\\:mm\\:ss}");
        Console.ResetColor();
    };
}
