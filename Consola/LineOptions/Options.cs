using CommandLine;

namespace Nauta.Cli.LineOptions;

internal class Options
{
    // --- Comandos de Acción ---
    [Option('l', "login", SetName = "action", HelpText = "Intenta iniciar sesión en el portal de Etecsa.")]
    public bool Login { get; set; }

    [Option('o', "logout", SetName = "action", HelpText = "Cierra la sesión activa actual.")]
    public bool Logout { get; set; }

    [Option('s', "status", SetName = "action", HelpText = "Muestra el estado de la conexión y tiempo restante.")]
    public bool Status { get; set; }

    // --- Parámetros de Credenciales ---
    [Option('u', "user", HelpText = "Nombre de usuario (ej. usuario@nauta.com.cu).")]
    public string? User { get; set; }

    [Option('p', "pass", HelpText = "Contraseña de la cuenta.")]
    public string? Password { get; set; }

    [Option('r', "remember", HelpText = "Guarda las credenciales de forma cifrada si el login es exitoso.")]
    public bool Remember { get; set; }

    // --- Utilidades ---
    [Option('v', "verbose", HelpText = "Muestra detalles técnicos de las peticiones HTTP.")]
    public bool Verbose { get; set; }
}
