using Nauta.Cli.LineOptions;
using NautaCredential.Contracts;
using NautaCredential.DTO;
using NautaManager.Contracts;

namespace Nauta.Cli.Handles;

internal class CommandHandler(
    INautaService service,
    ICredentialManager<UserCredentials> credential)
{
    public async Task ExecuteOptionsAsync(Options opts)
    {   
        // --- GESTION DE CUENTAS --- //
        
        // - Listar cuentas
        if (opts.ListAccounts)
        {
            ShowSavedCredentials();
            return;
        }

        // - Eliminar un usuario
        if (!string.IsNullOrEmpty(opts.RemoveUser))
        {
            credential.Delete(opts.RemoveUser);
            Console.WriteLine($"[*] Cuenta {opts.RemoveUser} eliminada.");
            return;
        }

        // - Establecer una cuenta como principal
        if (!string.IsNullOrEmpty(opts.SetDefault))
        {
            credential.SetDefault(opts.SetDefault);
            Console.WriteLine($"[*] {opts.SetDefault} ahora es la cuenta predeterminada.");
            return;
        }
        
        // --- CONEXION (ONLINE) --- //
        
        // - Restaurar una session activa
        if (opts.Status || opts.Logout)
        {
            await service.TryRestoreSessionAsync();
        }

        if (opts.Login)
        {
            await HandleLoginAsync(opts);
        }
        else if (opts.Logout)
        {
            await service.LogoutAsync();
        }
        else if (opts.Status)
        {
            await service.UpdateRemainingTimeAsync();
        } else if (opts.ListAccounts)
        {
            //ShowSavedCredentials
        }
    }
    
    private void ShowSavedCredentials()
    {
        (string defaultUsername, IReadOnlyCollection<string> credentials) = credential.ListCredentials();
    
        if (credentials.Count == 0) 
        {
            Console.WriteLine("No existe credenciales guardadas.");
            return;
        }

        Console.WriteLine("\nCuentas registradas:");
        Console.WriteLine("-------------------");

        foreach (string username in  credentials)
        {
            if (username == defaultUsername)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($" [X] {username} (Principal)");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($" [ ] {username}");
            }
        }
        Console.WriteLine("-------------------\n");
    }

    private async Task HandleLoginAsync(Options opts)
    {
        string username = opts.User ?? credential.Load()?.Username
            ?? PromptInput("Introduce el usuario: ");

        if (string.IsNullOrEmpty(username)) return;
        
        string? password = opts.Password;
        
        if (string.IsNullOrEmpty(password))
        {
            UserCredentials? saved = credential.Load();
            if (saved != null && saved.Username == username) 
                password = saved.Password;
            else
                password = PromptInput($"Introduce la contraseña para {username}: ", isPassword: true);
        }

        if (string.IsNullOrEmpty(password)) return;
        
        bool success = await service.LoginAsync(username, password);
        
        if (success && opts.Remember)
        {
            credential.Save(new UserCredentials(username, password));
        }
    }

    private static string PromptInput(string message, bool isPassword = false)
    {
        Console.Write(message);
        if (!isPassword) return Console.ReadLine() ?? string.Empty;

        // Lógica simple para ocultar asteriscos (opcional)
        string pass = string.Empty;
        ConsoleKeyInfo key;
        do
        {
            key = Console.ReadKey(true);
            if (key.Key != ConsoleKey.Backspace &&
                    key.Key != ConsoleKey.Enter)
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
}
