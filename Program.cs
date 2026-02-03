// ============================================================================
// MSFS 2024 Remote Buttons - Serveur Web
// Permet de piloter MSFS depuis n'importe quel navigateur sur le réseau
// ============================================================================

using MsfsRemoteButtons.Services;
using EmbedIO;
using System.IO;

namespace MsfsRemoteButtons;

class Program
{
    static SimConnectService? _simConnect;
    static WebServerService? _webServer;
    static bool _running = true;

    static void Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       MSFS 2024 REMOTE BUTTONS - SERVEUR WEB               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Créer les services
        _simConnect = new SimConnectService();
        _webServer = new WebServerService(_simConnect, 8080);

        // Démarrer le serveur web
        _webServer.Start();

        // 🔴 Connexion automatique à MSFS au démarrage
        Console.WriteLine("\n🔌 Tentative de connexion automatique à MSFS...");
        _simConnect.Connect();

        Console.WriteLine();
        Console.WriteLine("┌────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│  [C] Reconnecter à MSFS                                    │");
        Console.WriteLine("│  [D] Déconnecter de MSFS                                   │");
        Console.WriteLine("│  [R] Rafraîchir détection avion                            │");
        Console.WriteLine("│  [Q] Quitter                                               │");
        Console.WriteLine("└────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        // Thread pour recevoir les messages SimConnect
        var receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();

        // Boucle principale
        while (_running)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                HandleKey(key);
            }
            Thread.Sleep(50);
        }

        // Nettoyage
        _simConnect?.Dispose();
        _webServer?.Dispose();

        Console.WriteLine("\nAu revoir !");
    }

    static void HandleKey(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.C:
                _simConnect?.Connect();
                break;

            case ConsoleKey.D:
                _simConnect?.Disconnect();
                break;

            case ConsoleKey.R:
                Console.WriteLine("🔄 Rafraîchissement détection avion...");
                _simConnect?.RequestAircraftTitle();
                break;

            case ConsoleKey.Q:
                _running = false;
                break;
        }
    }

    static void ReceiveLoop()
    {
        while (_running)
        {
            try
            {
                _simConnect?.ReceiveMessages();
            }
            catch
            {
                // Ignorer les erreurs de réception
            }
            Thread.Sleep(10);
        }
    }
}
