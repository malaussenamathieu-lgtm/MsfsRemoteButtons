using EmbedIO;
using EmbedIO.Files;
using EmbedIO.WebSockets;
using MsfsRemoteButtons.Profiles;
using System.Text.Json;

namespace MsfsRemoteButtons.Services;

/// <summary>
/// Message WebSocket
/// </summary>
public class WsMessage
{
    public string Type { get; set; } = "";
    public object? Data { get; set; }
}

/// <summary>
/// Serveur Web avec WebSocket pour les clients distants
///
/// Architecture:
/// - Serveur HTTP (EmbedIO) sur le port 8080 pour servir les fichiers statiques (HTML/CSS/JS)
/// - Module WebSocket pour la communication temps réel bidirectionnelle
///
/// Flux de données:
/// - SimConnect → WebServerService → WebSocket → Navigateur (états des contrôles)
/// - Navigateur → WebSocket → WebServerService → SimConnect (commandes)
/// </summary>
public class WebServerService : IDisposable
{
    private WebServer? _server;                              // Serveur HTTP EmbedIO
    private SimConnectWebSocket? _wsModule;                  // Module WebSocket
    private readonly SimConnectService _simConnect;          // Référence au service SimConnect
    private readonly int _port;                              // Port HTTP (défaut: 8080)

    public WebServerService(SimConnectService simConnect, int port = 8080)
    {
        _simConnect = simConnect;
        _port = port;

        // S'abonner aux événements SimConnect pour les relayer vers les clients web
        _simConnect.ConnectionChanged += OnConnectionChanged;
        _simConnect.AircraftChanged += OnAircraftChanged;
        _simConnect.StateChanged += OnStateChanged;
        _simConnect.EnvironmentDataChanged += OnEnvironmentDataChanged;
    }

    /// <summary>
    /// Démarre le serveur web
    /// </summary>
    public void Start()
    {
        var webRoot = Path.Combine(AppContext.BaseDirectory, "Web");

        // Créer le dossier Web s'il n'existe pas
        if (!Directory.Exists(webRoot))
        {
            Directory.CreateDirectory(webRoot);
            Console.WriteLine($"⚠️ Dossier Web créé: {webRoot}");
            Console.WriteLine("   Copie les fichiers HTML/JS dedans !");
        }

        _wsModule = new SimConnectWebSocket("/ws", _simConnect);

        _server = new WebServer(o => o
            .WithUrlPrefix($"http://*:{_port}/")
            .WithMode(HttpListenerMode.EmbedIO))
            .WithModule(_wsModule)
            .WithStaticFolder("/", webRoot, true, m => m
                .WithContentCaching(false));

        _server.StateChanged += (s, e) =>
        {
            Console.WriteLine($"🌐 Serveur Web: {e.NewState}");
        };

        _server.RunAsync();

        Console.WriteLine($"🌐 Serveur démarré sur http://localhost:{_port}");
        Console.WriteLine($"   Depuis un autre PC: http://{GetLocalIP()}:{_port}");
    }

    /// <summary>
    /// Broadcast un message JSON à tous les clients WebSocket connectés
    /// </summary>
    public void Broadcast(WsMessage message)
    {
        _wsModule?.BroadcastMessage(message);
    }

    // ========================================================================
    // HANDLERS EVENEMENTS SIMCONNECT
    // ========================================================================
    // Ces méthodes transforment les événements SimConnect en messages WebSocket

    /// <summary>
    /// Relaye l'état de connexion MSFS vers les clients web
    /// </summary>
    private void OnConnectionChanged(bool connected)
    {
        Broadcast(new WsMessage
        {
            Type = "connection",
            Data = new { connected }
        });
    }

    /// <summary>
    /// Relaye le changement d'avion vers les clients web
    /// Inclut le profil complet pour que l'interface puisse reconstruire les boutons
    /// </summary>
    private void OnAircraftChanged(string aircraftTitle)
    {
        var profile = _simConnect.ActiveProfile;
        Broadcast(new WsMessage
        {
            Type = "aircraft",
            Data = new
            {
                title = aircraftTitle,
                profile = profile != null ? SerializeProfile(profile) : null
            }
        });
    }

    /// <summary>
    /// Relaye un changement d'état (SimVar) vers les clients web
    /// L'interface met à jour visuellement le bouton correspondant (LED, sélection, etc.)
    /// </summary>
    private void OnStateChanged(string commandId, double value)
    {
        Console.WriteLine($"[DEBUG] WebServer: Envoi état {commandId} = {value}");
        Broadcast(new WsMessage
        {
            Type = "state",
            Data = new { id = commandId, value }
        });
    }

    /// <summary>
    /// Relaye les données environnementales (OAT) vers les clients web
    /// </summary>
    private void OnEnvironmentDataChanged(double oat)
    {
        Broadcast(new WsMessage
        {
            Type = "environmentUpdate",
            Data = new { oat }
        });
    }

    /// <summary>
    /// Sérialise un profil en objet JSON pour l'envoi au client
    /// Ne garde que les propriétés nécessaires à l'interface web
    /// </summary>
    private object SerializeProfile(IAircraftProfile profile)
    {
        return new
        {
            id = profile.AircraftId,
            name = profile.AircraftName,
            description = profile.Description,
            categories = profile.Categories,
            commands = profile.Commands.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                category = c.Category,
                controlType = c.ControlType.ToString().ToLower(),
                hidden = c.Hidden,
                options = c.SelectorOptions?.Select(o => new
                {
                    label = o.Label,
                    simEvent = o.SimEvent,
                    value = o.Value
                })
            })
        };
    }

    /// <summary>
    /// Détecte l'adresse IP locale du PC sur le réseau
    ///
    /// Astuce: On crée une socket UDP vers une IP externe (8.8.8.8 = DNS Google)
    /// sans réellement envoyer de données. Le système choisit automatiquement
    /// l'interface réseau appropriée et on récupère son IP locale.
    /// C'est plus fiable que de parcourir toutes les interfaces réseau.
    /// </summary>
    private string GetLocalIP()
    {
        try
        {
            // Créer une socket UDP (ne nécessite pas de connexion réelle)
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, 0);
            // "Connecter" vers Google DNS (aucun paquet n'est envoyé en UDP)
            socket.Connect("8.8.8.8", 65530);
            // Récupérer l'IP locale choisie par le système
            var endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
            return endPoint?.Address.ToString() ?? "localhost";
        }
        catch
        {
            return "localhost";
        }
    }

    public void Dispose()
    {
        _server?.Dispose();
    }
}

/// <summary>
/// Module WebSocket pour la communication temps réel avec les clients
///
/// Messages reçus (client → serveur):
/// - { type: "command", data: { id: "nav_lights" } }          → Exécuter une commande
/// - { type: "command", data: { id: "flaps", simEvent: "FLAPS_2", value: 2 } } → Sélecteur
/// - { type: "getState" }                                      → Demander l'état complet
/// - { type: "ping" }                                          → Keepalive
///
/// Messages envoyés (serveur → client):
/// - { type: "connection", data: { connected: true } }         → État connexion MSFS
/// - { type: "aircraft", data: { title: "...", profile: {...} } } → Profil avion
/// - { type: "state", data: { id: "nav_lights", value: 1 } }   → État d'un contrôle
/// - { type: "pong" }                                          → Réponse keepalive
/// </summary>
public class SimConnectWebSocket : WebSocketModule
{
    private readonly SimConnectService _simConnect;

    public SimConnectWebSocket(string urlPath, SimConnectService simConnect)
        : base(urlPath, true)  // true = accepte les sous-protocoles
    {
        _simConnect = simConnect;
    }

    /// <summary>
    /// Callback: Message reçu d'un client
    /// </summary>
    protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
    {
        var json = System.Text.Encoding.UTF8.GetString(buffer);

        try
        {
            var message = JsonSerializer.Deserialize<WsMessage>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (message == null) return;

            await HandleMessage(context, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erreur WS: {ex.Message}");
        }
    }

    /// <summary>
    /// Callback: Nouveau client connecté
    /// On lui envoie immédiatement l'état complet (connexion, profil, états)
    /// </summary>
    protected override async Task OnClientConnectedAsync(IWebSocketContext context)
    {
        Console.WriteLine($"📱 Client connecté: {context.RemoteEndPoint}");
        await SendInitialState(context);
    }

    /// <summary>
    /// Callback: Client déconnecté
    /// </summary>
    protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
    {
        Console.WriteLine($"📱 Client déconnecté: {context.RemoteEndPoint}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Route les messages entrants vers le handler approprié
    /// </summary>
    private async Task HandleMessage(IWebSocketContext context, WsMessage message)
    {
        switch (message.Type)
        {
            case "command":
                HandleCommand(message.Data);
                break;

            case "getState":
                await SendInitialState(context);
                break;

            case "ping":
                await SendToClient(context, new WsMessage { Type = "pong" });
                break;
        }
    }

    /// <summary>
    /// Traite une commande reçue du client et la transmet à SimConnect
    /// </summary>
    private void HandleCommand(object? data)
    {
        if (data == null) return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var cmd = JsonSerializer.Deserialize<CommandData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (cmd != null)
            {
                Console.WriteLine($"[DEBUG] WebServer: Received command id={cmd.Id}, simEvent={cmd.SimEvent}, value={cmd.Value}");
                _simConnect.SendCommand(cmd.Id, cmd.SimEvent, cmd.Value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erreur commande: {ex.Message}");
        }
    }

    /// <summary>
    /// Envoie l'état complet à un client qui vient de se connecter
    ///
    /// Ordre d'envoi:
    /// 1. État de connexion MSFS
    /// 2. Profil de l'avion actuel (si connecté)
    /// 3. Tous les états de contrôles (valeurs SimVar)
    /// </summary>
    private async Task SendInitialState(IWebSocketContext context)
    {
        // 1. État de connexion MSFS
        await SendToClient(context, new WsMessage
        {
            Type = "connection",
            Data = new { connected = _simConnect.IsConnected }
        });

        // 2. Profil de l'avion actuel (si disponible)
        if (_simConnect.ActiveProfile != null)
        {
            await SendToClient(context, new WsMessage
            {
                Type = "aircraft",
                Data = new
                {
                    title = _simConnect.CurrentAircraftTitle,
                    profile = SerializeProfile(_simConnect.ActiveProfile)
                }
            });

            // 3. Tous les états de contrôles
            // Permet au client de mettre à jour l'UI immédiatement
            var states = _simConnect.GetAllStates();
            foreach (var state in states)
            {
                await SendToClient(context, new WsMessage
                {
                    Type = "state",
                    Data = new { id = state.Key, value = state.Value }
                });
            }

            // 4. Données environnementales (OAT) si disponibles
            var currentOAT = _simConnect.CurrentOAT;
            if (!double.IsNaN(currentOAT))
            {
                await SendToClient(context, new WsMessage
                {
                    Type = "environmentUpdate",
                    Data = new { oat = currentOAT }
                });
            }
        }
    }

    /// <summary>
    /// Sérialise un profil en objet anonyme pour JSON
    /// (Dupliqué de WebServerService pour éviter les dépendances)
    /// </summary>
    private object SerializeProfile(IAircraftProfile profile)
    {
        return new
        {
            id = profile.AircraftId,
            name = profile.AircraftName,
            description = profile.Description,
            categories = profile.Categories,
            commands = profile.Commands.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                category = c.Category,
                controlType = c.ControlType.ToString().ToLower(),
                hidden = c.Hidden,
                options = c.SelectorOptions?.Select(o => new
                {
                    label = o.Label,
                    simEvent = o.SimEvent,
                    value = o.Value
                })
            })
        };
    }

    /// <summary>
    /// Envoie un message JSON à un client spécifique
    /// </summary>
    private async Task SendToClient(IWebSocketContext context, WsMessage message)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase  // type, data au lieu de Type, Data
        });
        await SendAsync(context, json);
    }

    /// <summary>
    /// Envoie un message JSON à tous les clients connectés
    /// </summary>
    public void BroadcastMessage(WsMessage message)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        BroadcastAsync(json);
    }
}

/// <summary>
/// Structure pour désérialiser les commandes reçues du client
/// </summary>
public class CommandData
{
    public string Id { get; set; } = "";           // ID de la commande (ex: "nav_lights")
    public string? SimEvent { get; set; }          // Event spécifique pour sélecteurs (ex: "FLAPS_2")
    public uint? Value { get; set; }               // Valeur pour sélecteurs (ex: 2)
}
