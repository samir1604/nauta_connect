using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nauta.Cli.Handles;
using Nauta.Cli.LineOptions;
using NautaCredential.Contracts;
using NautaCredential.DTO;
using NautaManager.Contracts;

namespace Nauta.Cli.ConfigureParser;

static internal class ConfigureCommandLineParser
{
    public static async Task Configure(IHost host, string[] args)
    {
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
                var nautaService = host.Services.GetRequiredService<INautaService>();
                var credentialManager = host.Services.GetRequiredService<ICredentialManager<UserCredentials>>();

                SetupConsoleEvents(nautaService);

                var handler = new CommandHandler(nautaService, credentialManager);
                await handler.ExecuteOptionsAsync(opts);
            });
    }

    private static void SetupConsoleEvents(INautaService nautaService)
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
}
