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
/// </summary>
public class WebServerService : IDisposable
{
    private WebServer? _server;
    private SimConnectWebSocket? _wsModule;
    private readonly SimConnectService _simConnect;
    private readonly int _port;

    public WebServerService(SimConnectService simConnect, int port = 8080)
    {
        _simConnect = simConnect;
        _port = port;

        // S'abonner aux événements SimConnect
        _simConnect.ConnectionChanged += OnConnectionChanged;
        _simConnect.AircraftChanged += OnAircraftChanged;
        _simConnect.StateChanged += OnStateChanged;
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
    /// Broadcast un message à tous les clients
    /// </summary>
    public void Broadcast(WsMessage message)
    {
        _wsModule?.BroadcastMessage(message);
    }

    private void OnConnectionChanged(bool connected)
    {
        Broadcast(new WsMessage
        {
            Type = "connection",
            Data = new { connected }
        });
    }

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

    private void OnStateChanged(string commandId, double value)
    {
        Broadcast(new WsMessage
        {
            Type = "state",
            Data = new { id = commandId, value }
        });
    }

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

    private string GetLocalIP()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
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
/// Module WebSocket pour la communication avec les clients
/// </summary>
public class SimConnectWebSocket : WebSocketModule
{
    private readonly SimConnectService _simConnect;

    public SimConnectWebSocket(string urlPath, SimConnectService simConnect)
        : base(urlPath, true)
    {
        _simConnect = simConnect;
    }

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

    protected override async Task OnClientConnectedAsync(IWebSocketContext context)
    {
        Console.WriteLine($"📱 Client connecté: {context.RemoteEndPoint}");

        // Envoyer l'état initial
        await SendInitialState(context);
    }

    protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
    {
        Console.WriteLine($"📱 Client déconnecté: {context.RemoteEndPoint}");
        return Task.CompletedTask;
    }

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
                _simConnect.SendCommand(cmd.Id, cmd.SimEvent);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erreur commande: {ex.Message}");
        }
    }

    private async Task SendInitialState(IWebSocketContext context)
    {
        // Envoyer l'état de connexion
        await SendToClient(context, new WsMessage
        {
            Type = "connection",
            Data = new { connected = _simConnect.IsConnected }
        });

        // Envoyer le profil actif
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

            // Envoyer tous les états
            var states = _simConnect.GetAllStates();
            foreach (var state in states)
            {
                await SendToClient(context, new WsMessage
                {
                    Type = "state",
                    Data = new { id = state.Key, value = state.Value }
                });
            }
        }
    }

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

    private async Task SendToClient(IWebSocketContext context, WsMessage message)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await SendAsync(context, json);
    }

    public void BroadcastMessage(WsMessage message)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        BroadcastAsync(json);
    }
}

public class CommandData
{
    public string Id { get; set; } = "";
    public string? SimEvent { get; set; }
}
