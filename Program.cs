// ============================================================================
// MSFS 2024 Remote Buttons - Serveur Web
// Permet de piloter MSFS depuis n'importe quel navigateur sur le réseau
// ============================================================================
//
// Architecture:
// - SimConnectService: Communication avec MSFS via SimConnect
// - WebServerService: Serveur HTTP/WebSocket pour les clients web
// - ProfileManager: Gestion des profils avion (détection auto)
//
// Flux:
// 1. Démarrage du serveur web (port 8080)
// 2. Connexion automatique à MSFS
// 3. Boucle de réception des messages SimConnect (thread séparé)
// 4. Boucle principale: gestion des commandes clavier console
//
// ============================================================================

using MsfsRemoteButtons.Services;
using EmbedIO;
using System.IO;

namespace MsfsRemoteButtons;

class Program
{
    // === SERVICES ===
    static SimConnectService? _simConnect;   // Interface MSFS
    static WebServerService? _webServer;     // Serveur HTTP/WebSocket
    static bool _running = true;             // Flag pour arrêter les boucles

    // Console key input: désactivé si pas de console (redirect, IDE, etc.)
    static bool _consoleInputUnavailable;
    static bool _consoleInputWarningLogged;

    /// <summary>
    /// Point d'entrée de l'application
    /// </summary>
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

        // Thread séparé pour recevoir les messages SimConnect
        // IMPORTANT: SimConnect.ReceiveMessage() doit être appelé régulièrement
        // sinon les callbacks (OnRecvSimobjectData, etc.) ne sont jamais déclenchés
        var receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();

        // Boucle principale: gestion des commandes clavier
        // Le serveur web tourne en arrière-plan via EmbedIO
        while (_running)
        {
            if (!_consoleInputUnavailable)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;  // true = ne pas afficher la touche
                        HandleKey(key);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Pas de console (redirect, IDE, etc.) : désactiver la lecture clavier
                    _consoleInputUnavailable = true;
                    if (!_consoleInputWarningLogged)
                    {
                        Console.WriteLine("⚠️ Entrée clavier indisponible (console absente ou redirigée). Touches [C],[D],[R],[Q] désactivées.");
                        _consoleInputWarningLogged = true;
                    }
                }
            }
            Thread.Sleep(50);  // Éviter de consommer 100% CPU
        }

        // Nettoyage propre avant de quitter
        _simConnect?.Dispose();
        _webServer?.Dispose();

        Console.WriteLine("\nAu revoir !");
    }

    /// <summary>
    /// Gère les commandes clavier de la console
    /// </summary>
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

    /// <summary>
    /// Boucle de réception des messages SimConnect (thread séparé)
    ///
    /// SimConnect fonctionne en mode polling: on doit appeler ReceiveMessage()
    /// régulièrement pour que les callbacks soient déclenchés.
    /// Cette boucle tourne toutes les 10ms pour une réactivité optimale.
    /// </summary>
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
                // Ignorer les erreurs de réception (déconnexion gérée ailleurs)
            }
            Thread.Sleep(10);  // 100 Hz de polling
        }
    }
}
