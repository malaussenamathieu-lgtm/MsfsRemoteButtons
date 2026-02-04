using Microsoft.FlightSimulator.SimConnect;
using MsfsRemoteButtons.Profiles;
using System.Runtime.InteropServices;
using System.Linq;

namespace MsfsRemoteButtons.Services;

/// <summary>
/// Données pour une SimVar
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct SimVarData
{
    public double Value;
}

/// <summary>
/// Données pour le titre de l'avion
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct AircraftTitleData
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Title;
}

/// <summary>
/// Service de gestion SimConnect
/// </summary>
public class SimConnectService : IDisposable
{
    private SimConnect? _simConnect;
    private bool _isConnected;
    private IAircraftProfile? _activeProfile;
    private readonly Dictionary<string, int> _eventIds = new();
    private readonly Dictionary<int, string> _simVarDefinitions = new(); // defId -> commandId
    private int _nextEventId = 1;
    private int _nextDefinitionId = 100;

    private const int WM_USER_SIMCONNECT = 0x0402;
    private const int AIRCRAFT_TITLE_DEFINITION = 1;
    private const int AIRCRAFT_TITLE_REQUEST = 1;

    // États des boutons
    private readonly Dictionary<string, double> _buttonStates = new();
    private readonly object _stateLock = new();

    // Événements
    public event Action<bool>? ConnectionChanged;
    public event Action<string>? AircraftChanged;
    public event Action<string, double>? StateChanged;
    public event Action<string>? LogMessage;

    public bool IsConnected => _isConnected;
    public IAircraftProfile? ActiveProfile => _activeProfile;
    public string CurrentAircraftTitle { get; private set; } = "";

    /// <summary>
    /// Tente de se connecter à MSFS
    /// </summary>
    public bool Connect()
    {
        if (_isConnected) return true;

        try
        {
            Log("Tentative de connexion à MSFS 2024...");
            _simConnect = new SimConnect("MSFS Remote Buttons", IntPtr.Zero, WM_USER_SIMCONNECT, null, 0);

            // Callbacks
            _simConnect.OnRecvOpen += OnRecvOpen;
            _simConnect.OnRecvQuit += OnRecvQuit;
            _simConnect.OnRecvException += OnRecvException;
            _simConnect.OnRecvSimobjectData += OnRecvSimobjectData;

            // Enregistrer la requête pour le titre de l'avion
            _simConnect.AddToDataDefinition(
                (DefineId)AIRCRAFT_TITLE_DEFINITION,
                "TITLE",
                null,
                SIMCONNECT_DATATYPE.STRING256,
                0,
                SimConnect.SIMCONNECT_UNUSED
            );
            _simConnect.RegisterDataDefineStruct<AircraftTitleData>((DefineId)AIRCRAFT_TITLE_DEFINITION);

            _isConnected = true;
            Log("✅ Connecté à MSFS 2024");
            ConnectionChanged?.Invoke(true);

            // Demander le titre de l'avion
            RequestAircraftTitle();

            return true;
        }
        catch (COMException ex)
        {
            Log($"❌ Erreur de connexion: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Déconnexion
    /// </summary>
    public void Disconnect()
    {
        if (_simConnect != null)
        {
            _simConnect.Dispose();
            _simConnect = null;
        }
        _isConnected = false;
        _eventIds.Clear();
        _simVarDefinitions.Clear();
        _buttonStates.Clear();
        _nextEventId = 1;
        _nextDefinitionId = 100;
        CurrentAircraftTitle = "";
        _activeProfile = null;

        Log("🔌 Déconnecté de MSFS");
        ConnectionChanged?.Invoke(false);
    }

    /// <summary>
    /// Doit être appelé régulièrement pour traiter les messages SimConnect
    /// </summary>
    public void ReceiveMessages()
    {
        if (_simConnect == null || !_isConnected) return;

        try
        {
            _simConnect.ReceiveMessage();
        }
        catch (Exception)
        {
            // Connexion perdue
            Disconnect();
        }
    }

    /// <summary>
    /// Demande le titre de l'avion actuel
    /// </summary>
    public void RequestAircraftTitle()
    {
        if (_simConnect == null || !_isConnected) return;

        _simConnect.RequestDataOnSimObject(
            (RequestId)AIRCRAFT_TITLE_REQUEST,
            (DefineId)AIRCRAFT_TITLE_DEFINITION,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0, 0, 0
        );
    }

    /// <summary>
    /// Change le profil actif et enregistre les SimVars/Events
    /// </summary>
    public void SetProfile(IAircraftProfile profile)
    {
        _activeProfile = profile;
        RegisterProfileEvents();
        RegisterProfileSimVars();

        // Exporter les SimEvents après le chargement du profil
        try
        {
            profile.ExportSimEventsToFile();
            Log($"📄 SimEvents exportés");
        }
        catch (Exception ex)
        {
            Log($"❌ Erreur export SimEvents: {ex.Message}");
        }

        Log($"✈️ Profil chargé: {profile.AircraftName}");
    }

    /// <summary>
    /// Envoie une commande
    /// </summary>
    public void SendCommand(string commandId, string? simEvent = null, uint? value = null)
    {
        if (_simConnect == null || !_isConnected || _activeProfile == null) return;

        // Si simEvent est fourni (pour les sélecteurs), l'utiliser avec la valeur
        if (!string.IsNullOrEmpty(simEvent))
        {
            SendEventByName(simEvent, value: value ?? 0, momentary: false);
            return;
        }

        // Gestion spéciale pour les incréments multiples (HDG +/-10, ALT +/-1000)
        int repeatCount = 1;
        int delayMs = 50;
        string? actualCommandId = commandId;

        if (commandId == "hdg_inc_10") { actualCommandId = "hdg_inc_1"; repeatCount = 10; delayMs = 50; }
        else if (commandId == "hdg_dec_10") { actualCommandId = "hdg_dec_1"; repeatCount = 10; delayMs = 50; }
        else if (commandId == "alt_inc_1000") { actualCommandId = "alt_inc_100"; repeatCount = 10; delayMs = 100; }
        else if (commandId == "alt_dec_1000") { actualCommandId = "alt_dec_100"; repeatCount = 10; delayMs = 100; }

        // Récupérer la commande
        var command = _activeProfile.Commands.FirstOrDefault(c => c.Id == actualCommandId);
        if (command == null) return;

        // Exécuter plusieurs fois si nécessaire (avec délai pour que MSFS traite chaque event)
        for (int i = 0; i < repeatCount; i++)
        {
            ExecuteCommand(command, actualCommandId);
            if (repeatCount > 1 && i < repeatCount - 1)
            {
                Thread.Sleep(delayMs);
            }
        }
    }

    private void ExecuteCommand(AircraftCommand command, string commandId)
    {
        // Si SimEventOn/Off sont définis, utiliser l'état courant pour choisir
        if (!string.IsNullOrEmpty(command.SimEventOn) && !string.IsNullOrEmpty(command.SimEventOff))
        {
            var currentState = GetState(commandId);
            if (currentState > 0.5)
            {
                // État ON → envoyer OFF avec valeur 0
                SendEventByName(command.SimEventOff, value: 0, momentary: command.IsMomentary);
                Log($"→ {commandId} ({command.SimEventOff})");
            }
            else
            {
                // État OFF → envoyer ON avec valeur 1
                SendEventByName(command.SimEventOn, value: 1, momentary: command.IsMomentary);
                Log($"→ {commandId} ({command.SimEventOn})");
            }
            RefreshSimVarForCommand(commandId);
            return;
        }

        // Sinon, chercher l'event dans le profil
        if (_eventIds.TryGetValue(commandId, out int eventId))
        {
            try
            {
                bool momentary = command.IsMomentary;

                if (momentary)
                {
                    _simConnect.TransmitClientEvent(0, (EventId)eventId, 1, NotificationGroup.Group0, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                    _simConnect.TransmitClientEvent(0, (EventId)eventId, 0, NotificationGroup.Group0, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                }
                else
                {
                    _simConnect.TransmitClientEvent(0, (EventId)eventId, 0, NotificationGroup.Group0, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                }

                Log($"→ {commandId}");
                RefreshSimVarForCommand(commandId);
            }
            catch (Exception ex)
            {
                Log($"❌ Erreur envoi commande: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Force la re-demande d'une SimVar après envoi de commande
    /// </summary>
    private void RefreshSimVarForCommand(string commandId)
    {
        if (_simConnect == null || _activeProfile == null) return;

        var command = _activeProfile.Commands.FirstOrDefault(c => c.Id == commandId);
        if (command == null || string.IsNullOrEmpty(command.SimVar)) return;

        try
        {
            // Chercher la defId associée à cette command
            var defId = _simVarDefinitions.FirstOrDefault(x => x.Value == commandId).Key;
            if (defId > 0)
            {
                // Re-demander les données immédiatement
                _simConnect.RequestDataOnSimObject(
                    (RequestId)defId,
                    (DefineId)defId,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.ONCE,
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                    0, 0, 0
                );
            }
        }
        catch (Exception ex)
        {
            Log($"⚠️ Erreur refresh SimVar pour {commandId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Envoie un événement par son nom SimConnect avec valeur optionnelle
    /// </summary>
    private void SendEventByName(string simEvent, uint value = 0, bool momentary = false)
    {
        if (_simConnect == null) return;

        try
        {
            int eventId = _nextEventId++;
            _simConnect.MapClientEventToSimEvent((EventId)eventId, simEvent);

            if (momentary)
            {
                _simConnect.TransmitClientEvent(0, (EventId)eventId, 1, NotificationGroup.Group0, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                _simConnect.TransmitClientEvent(0, (EventId)eventId, 0, NotificationGroup.Group0, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
            }
            else
            {
                _simConnect.TransmitClientEvent(0, (EventId)eventId, value, NotificationGroup.Group0, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
            }

            Log($"→ {simEvent}");
        }
        catch (Exception ex)
        {
            Log($"❌ Erreur envoi {simEvent}: {ex.Message}");
        }
    }

    /// <summary>
    /// Récupère l'état actuel d'un bouton
    /// </summary>
    public double GetState(string commandId)
    {
        lock (_stateLock)
        {
            return _buttonStates.TryGetValue(commandId, out var value) ? value : 0;
        }
    }

    /// <summary>
    /// Récupère tous les états
    /// </summary>
    public Dictionary<string, double> GetAllStates()
    {
        lock (_stateLock)
        {
            return new Dictionary<string, double>(_buttonStates);
        }
    }

    // ========================================================================
    // ENREGISTREMENT PROFIL
    // ========================================================================

    private void RegisterProfileEvents()
    {
        if (_simConnect == null || _activeProfile == null) return;

        _eventIds.Clear();

        foreach (var command in _activeProfile.Commands)
        {
            if (string.IsNullOrEmpty(command.SimEvent)) continue;

            try
            {
                int eventId = _nextEventId++;
                _eventIds[command.Id] = eventId;
                _simConnect.MapClientEventToSimEvent((EventId)eventId, command.SimEvent);
                Log($"   ✓ {command.SimEvent} ({command.Id})");  // Log chaque événement mappé
            }
            catch (Exception ex)
            {
                Log($"⚠️ Erreur mapping {command.Name} ({command.SimEvent}): {ex.Message}");
            }
        }

        Log($"   → {_eventIds.Count} événements enregistrés");
    }

    private void RegisterProfileSimVars()
    {
        if (_simConnect == null || _activeProfile == null) return;

        _simVarDefinitions.Clear();

        foreach (var command in _activeProfile.Commands)
        {
            if (string.IsNullOrEmpty(command.SimVar)) continue;

            try
            {
                int defId = _nextDefinitionId++;
                _simVarDefinitions[defId] = command.Id;

                _simConnect.AddToDataDefinition(
                    (DefineId)defId,
                    command.SimVar,
                    command.SimVarUnit ?? "Bool",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0,
                    SimConnect.SIMCONNECT_UNUSED
                );
                _simConnect.RegisterDataDefineStruct<SimVarData>((DefineId)defId);

                // Demander les mises à jour automatiques
                _simConnect.RequestDataOnSimObject(
                    (RequestId)defId,
                    (DefineId)defId,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.VISUAL_FRAME,
                    SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
                    0, 0, 0
                );
            }
            catch (Exception ex)
            {
                Log($"⚠️ Erreur SimVar {command.SimVar}: {ex.Message}");
            }
        }

        Log($"   → {_simVarDefinitions.Count} SimVars enregistrées");
    }

    // ========================================================================
    // CALLBACKS SIMCONNECT
    // ========================================================================

    private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        Log($"   → MSFS: {data.szApplicationName}");
    }

    private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Log("⚠️ MSFS fermé");
        Disconnect();
    }

    private void OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        string errorInfo = data.dwException switch
        {
            7 => "UNRECOGNIZED_ID (événement/ID invalide)",
            8 => "UNDEFINED_ID (ID non défini)",
            10 => "INVALID_DATA_TYPE (type de données invalide)",
            _ => $"Code: {data.dwException}"
        };

        Log($"⚠️ Exception SimConnect: {errorInfo}");
    }

    private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        // Titre de l'avion
        if (data.dwRequestID == AIRCRAFT_TITLE_REQUEST)
        {
            var titleData = (AircraftTitleData)data.dwData[0];
            if (titleData.Title != CurrentAircraftTitle)
            {
                CurrentAircraftTitle = titleData.Title;
                Log($"🛫 Avion détecté: {CurrentAircraftTitle}");

                // Auto-détection du profil
                var profile = ProfileManager.DetectProfile(CurrentAircraftTitle);
                if (profile != null)
                {
                    SetProfile(profile);
                }
                else
                {
                    Log($"⚠️ Aucun profil trouvé, utilisation du profil par défaut");
                    SetProfile(ProfileManager.DefaultProfile);
                }

                AircraftChanged?.Invoke(CurrentAircraftTitle);
            }
            return;
        }

        // SimVar data
        int requestId = (int)data.dwRequestID;
        if (_simVarDefinitions.TryGetValue(requestId, out var commandId))
        {
            var simVarData = (SimVarData)data.dwData[0];

            lock (_stateLock)
            {
                var oldValue = _buttonStates.GetValueOrDefault(commandId);
                if (Math.Abs(oldValue - simVarData.Value) > 0.001)
                {
                    _buttonStates[commandId] = simVarData.Value;
                    StateChanged?.Invoke(commandId, simVarData.Value);
                }
            }
        }
    }

    private void Log(string message)
    {
        Console.WriteLine(message);
        LogMessage?.Invoke(message);
    }

    public void Dispose()
    {
        Disconnect();
    }
}

// Enums pour SimConnect
enum DefineId { }
enum RequestId { }
enum EventId { }
enum NotificationGroup { Group0 = 0 }
