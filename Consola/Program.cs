// See https://aka.ms/new-console-template for more information
using NautaManager;

Console.WriteLine("Haciendo prueba de conexion con etecsa");

var connect = new NautaConnect();

var initData = await connect.GetInitialData();

if(initData != null)
{
    string user = "samir1604@nauta.com.cu";
    string pass = "1234";    

    bool conectado = await connect.Login(user, pass, initData);

    if (conectado)
    {
        int segundosRestantes = 60; // Prueba de 1 minuto
        Console.WriteLine($"La sesión se cerrará automáticamente en {segundosRestantes} segundos...");

        // Usamos un bucle simple para simular el tiempo
        while (segundosRestantes > 0)
        {
            Thread.Sleep(1000);
            segundosRestantes--;
            if (segundosRestantes % 10 == 0)
                Console.WriteLine($"Quedan {segundosRestantes}s...");
        }

        // Al terminar el tiempo, ejecutamos el Logout
        await connect.Logout(user, initData);
    }
}
