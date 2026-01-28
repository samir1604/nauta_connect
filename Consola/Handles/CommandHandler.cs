using Nauta.Cli.LineOptions;
using NautaCredential.Contracts;
using NautaCredential.DTO;
using NautaManager.Contracts;

namespace Nauta.Cli.Handles;

internal class CommandHandler(
    INautaService nautaService,
    ICredentialManager<UserCredentials> credentialManager)
{
    private readonly INautaService _nauta = nautaService;
    private readonly ICredentialManager<UserCredentials> _credential = credentialManager;

    public async Task ExecuteOptionsAsync(Options opts)
    {
        if (opts.Status || opts.Logout)
        {
            await _nauta.TryRestoreSessionAsync();
        }

        if (opts.Login)
        {
            await HandleLoginAsync(opts);
        }
        else if (opts.Logout)
        {
            await _nauta.LogoutAsync();
        }
        else if (opts.Status)
        {
            await _nauta.UpdateRemainingTimeAsync();
        }
    }

    private async Task HandleLoginAsync(Options opts)
    {
        // 1. Obtener usuario (Prioridad: Parámetro > Disco > Consola)
        string username = opts.User ?? _credential.Load()?.Username
            ?? PromptInput("Introduce el usuario: ");

        if (string.IsNullOrEmpty(username)) return;

        // 2. Obtener contraseña
        string? password = opts.Password;

        // Si no vino por parámetro, intentamos cargarla del disco SOLO si el usuario coincide
        if (string.IsNullOrEmpty(password))
        {
            var saved = _credential.Load();
            if (saved != null && saved.Username == username)
            {
                password = saved.Password;
            }
            else
            {
                // Si no coincide o no hay, preguntamos
                password = PromptInput($"Introduce la contraseña para {username}: ", isPassword: true);
            }
        }

        if (string.IsNullOrEmpty(password)) return;

        // 3. Ejecutar Login
        bool success = await _nauta.LoginAsync(username, password);

        // 4. Recordar si se solicitó
        if (success && opts.Remember)
        {
            _credential.Save(new UserCredentials(username, password));
        }
    }

    private string PromptInput(string message, bool isPassword = false)
    {
        Console.Write(message);
        if (!isPassword) return Console.ReadLine() ?? string.Empty;

        // Lógica simple para ocultar asteriscos (opcional)
        string pass = string.Empty;
        ConsoleKeyInfo key;
        do
        {
            key = Console.ReadKey(true);
            if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
            {
                pass += key.KeyChar;
                Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
            {
                pass = pass[..^1];
                Console.Write("\b \b");
            }
        } while (key.Key != ConsoleKey.Enter);
        Console.WriteLine();
        return pass;
    }



    public string RequestUserFromConsole()
    {
        Console.Write("Introduce el usuario: ");
        return Console.ReadLine() ?? string.Empty;
    }
}
