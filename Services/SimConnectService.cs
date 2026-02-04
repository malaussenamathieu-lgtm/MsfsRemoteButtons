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
/// Service de gestion SimConnect - Interface principale avec MSFS 2024
///
/// Ce service gère:
/// - La connexion/déconnexion avec MSFS via COM SimConnect
/// - L'envoi de commandes (K:Events) vers le simulateur
/// - La lecture des états (SimVars) depuis le simulateur
/// - La détection automatique de l'avion et le chargement du profil
///
/// Architecture:
/// - SimConnect utilise un système de callbacks pour recevoir les données
/// - Les events sont mappés à des IDs uniques lors de l'enregistrement
/// - Les SimVars sont lues en continu (VISUAL_FRAME) avec notification sur changement
/// </summary>
public class SimConnectService : IDisposable
{
    // === CONNEXION SIMCONNECT ===
    private SimConnect? _simConnect;                                        // Instance SimConnect (null si déconnecté)
    private bool _isConnected;                                              // État de connexion
    private IAircraftProfile? _activeProfile;                               // Profil d'avion actuellement chargé

    // === MAPPINGS EVENTS/SIMVARS ===
    // SimConnect nécessite des IDs numériques pour référencer events et SimVars
    private readonly Dictionary<string, int> _eventIds = new();             // commandId -> eventId (pour envoyer des commandes)
    private readonly Dictionary<int, string> _simVarDefinitions = new();    // defId -> commandId (pour recevoir des états)
    private int _nextEventId = 1;                                           // Compteur auto-incrémenté pour les events
    private int _nextDefinitionId = 100;                                    // Compteur pour les définitions SimVar (commence à 100 pour éviter collision avec AIRCRAFT_TITLE)

    // === CONSTANTES SIMCONNECT ===
    // WM_USER_SIMCONNECT: Message Windows personnalisé pour SimConnect
    // Valeur standard 0x0402 (WM_USER + 2) utilisée par convention SimConnect
    private const int WM_USER_SIMCONNECT = 0x0402;

    // IDs réservés pour la détection de l'avion (utilisés avant le chargement du profil)
    private const int AIRCRAFT_TITLE_DEFINITION = 1;                        // ID de définition pour le titre
    private const int AIRCRAFT_TITLE_REQUEST = 1;                           // ID de requête pour le titre

    // === ÉTATS DES CONTRÔLES ===
    // Cache local des valeurs SimVar pour éviter les requêtes répétées
    private readonly Dictionary<string, double> _buttonStates = new();      // commandId -> valeur actuelle (0.0 ou 1.0 pour Bool)
    private readonly object _stateLock = new();                             // Verrou pour accès thread-safe

    // THREAD-SAFETY: SimConnect n'est pas thread-safe (documentation Microsoft). Tous les appels _simConnect.* doivent être dans lock (_simConnectLock).
    private readonly object _simConnectLock = new();

    // === ÉVÉNEMENTS PUBLICS ===
    // Ces événements permettent aux autres services (WebServer) de réagir aux changements
    public event Action<bool>? ConnectionChanged;           // Déclenché quand connexion/déconnexion MSFS
    public event Action<string>? AircraftChanged;           // Déclenché quand l'avion change (nouveau titre détecté)
    public event Action<string, double>? StateChanged;      // Déclenché quand une SimVar change (commandId, nouvelle valeur)
    public event Action<string>? LogMessage;                // Déclenché pour afficher un message dans la console

    // === PROPRIÉTÉS PUBLIQUES ===
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
    /// Envoie une commande vers MSFS
    ///
    /// Cas d'utilisation:
    /// 1. Commande simple (Toggle): SendCommand("nav_lights")
    /// 2. Sélecteur avec event spécifique: SendCommand("flaps", simEvent: "FLAPS_2", value: 2)
    /// 3. Incrément multiple: SendCommand("hdg_inc_10") → exécute 10x hdg_inc_1
    /// </summary>
    /// <param name="commandId">ID de la commande définie dans le profil</param>
    /// <param name="simEvent">Event SimConnect à exécuter directement (pour sélecteurs)</param>
    /// <param name="value">Valeur à passer à l'event (pour sélecteurs)</param>
    public void SendCommand(string commandId, string? simEvent = null, uint? value = null)
    {
        if (_simConnect == null || !_isConnected || _activeProfile == null) return;

        // === CAS 1: Event direct (sélecteurs) ===
        // L'interface web envoie directement le SimEvent à exécuter (ex: FLAPS_2)
        if (!string.IsNullOrEmpty(simEvent))
        {
            SendEventByName(simEvent, value: value ?? 0, momentary: false);
            return;
        }

        // === CAS 2: Incréments multiples ===
        // Les boutons +10 ou +1000 n'existent pas dans SimConnect, donc on répète
        // la commande unitaire plusieurs fois avec un délai entre chaque.
        // IMPORTANT: Le délai est nécessaire car MSFS ne peut pas traiter les events trop vite
        int repeatCount = 1;
        int delayMs = 50;
        string? actualCommandId = commandId;

        // Mapping commandes multiples → commande unitaire + nombre de répétitions
        if (commandId == "hdg_inc_10") { actualCommandId = "hdg_inc_1"; repeatCount = 10; delayMs = 50; }
        else if (commandId == "hdg_dec_10") { actualCommandId = "hdg_dec_1"; repeatCount = 10; delayMs = 50; }
        else if (commandId == "alt_inc_1000") { actualCommandId = "alt_inc_100"; repeatCount = 10; delayMs = 100; }  // ALT plus lent car calculs plus complexes
        else if (commandId == "alt_dec_1000") { actualCommandId = "alt_dec_100"; repeatCount = 10; delayMs = 100; }

        // === CAS 3: Commande simple ===
        var command = _activeProfile.Commands.FirstOrDefault(c => c.Id == actualCommandId);
        if (command == null) return;

        // Exécution avec délai entre chaque répétition
        for (int i = 0; i < repeatCount; i++)
        {
            ExecuteCommand(command, actualCommandId);
            if (repeatCount > 1 && i < repeatCount - 1)
            {
                Thread.Sleep(delayMs);  // Laisser le temps à MSFS de traiter
            }
        }
    }

    /// <summary>
    /// Exécute une commande vers MSFS
    ///
    /// Gère deux types de commandes:
    /// 1. Commandes avec SimEventOn/SimEventOff séparés (ex: fuel pump)
    ///    → On lit l'état actuel et on envoie l'event inverse
    /// 2. Commandes avec SimEvent simple (ex: TOGGLE_NAV_LIGHTS)
    ///    → On envoie directement l'event
    ///
    /// Le flag IsMomentary simule un appui physique (press puis release)
    /// nécessaire pour certains interrupteurs dans MSFS
    /// </summary>
    private void ExecuteCommand(AircraftCommand command, string commandId)
    {
        // === CAS 1: Events ON/OFF séparés ===
        // Certains systèmes (fuel pump) nécessitent des events distincts pour ON et OFF
        // On doit lire l'état actuel pour savoir quel event envoyer
        if (!string.IsNullOrEmpty(command.SimEventOn) && !string.IsNullOrEmpty(command.SimEventOff))
        {
            var currentState = GetState(commandId);
            if (currentState > 0.5)
            {
                // Actuellement ON → envoyer OFF
                SendEventByName(command.SimEventOff, value: 0, momentary: command.IsMomentary);
                Log($"→ {commandId} ({command.SimEventOff})");
            }
            else
            {
                // Actuellement OFF → envoyer ON
                SendEventByName(command.SimEventOn, value: 1, momentary: command.IsMomentary);
                Log($"→ {commandId} ({command.SimEventOn})");
            }
            RefreshSimVarForCommand(commandId);  // Forcer la relecture de l'état
            return;
        }

        // === CAS 2: Event simple (Toggle) ===
        if (_eventIds.TryGetValue(commandId, out int eventId) && _simConnect != null)
        {
            try
            {
                bool momentary = command.IsMomentary;

                if (momentary)
                {
                    // Simuler un appui physique: envoyer valeur 1 (press) puis 0 (release)
                    // Nécessaire pour certains interrupteurs qui réagissent au front montant
                    _simConnect.TransmitClientEvent(0, (EventId)eventId, 1, NotificationGroup.Group0, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                    _simConnect.TransmitClientEvent(0, (EventId)eventId, 0, NotificationGroup.Group0, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                }
                else
                {
                    // Envoi simple de l'event (la plupart des TOGGLE_* fonctionnent ainsi)
                    _simConnect.TransmitClientEvent(0, (EventId)eventId, 0, NotificationGroup.Group0, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                }

                Log($"→ {commandId}");
                RefreshSimVarForCommand(commandId);  // Forcer la relecture de l'état
            }
            catch (Exception ex)
            {
                Log($"❌ Erreur envoi commande: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Force la re-demande d'une SimVar après envoi de commande
    ///
    /// Pourquoi: Après avoir envoyé un event, MSFS met à jour l'état interne
    /// mais la notification automatique (SIMCONNECT_DATA_REQUEST_FLAG.CHANGED)
    /// peut avoir un délai. Cette méthode force une relecture immédiate.
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
    /// Envoie une valeur à un B: event (Input Event) identifié par son hash.
    /// Équivalent SimConnect_SetInputEvent : définit la valeur d'un input event sans générer d'event de réponse.
    /// Gestion erreurs : le wrapper managé lève COMException ; les codes SIMCONNECT_EXCEPTION (ex. GET_INPUT_EVENT_FAILED) sont documentés dans le SDK.
    /// </summary>
    /// <param name="hash">Hash de l'event (obtenu via EnumerateInputEvents).</param>
    /// <param name="value">Valeur à envoyer (ex: 0.0, 1.0 pour booléen; valeur FLOAT64 pour sélecteurs).</param>
    // B: EVENT: Uses modern Input Events API (SetInputEvent)
    // THREAD-SAFETY: Protected by _simConnectLock
    public void SetInputEvent(ulong hash, double value)
    {
        if (_simConnect == null || !_isConnected) return;

        lock (_simConnectLock)
        {
            try
            {
                _simConnect.SetInputEvent(hash, value);

#if DEBUG
                Log($"[DEBUG] SetInputEvent: hash={hash} value={value}");
#endif
            }
            catch (COMException ex)
            {
                // Échec HRESULT SimConnect (hash invalide, erreur interne) — doc SDK SimConnect_SetInputEvent
                Log($"❌ SetInputEvent (hash={hash}): {ex.Message}");
            }
            catch (Exception ex)
            {
                // Gestion SimConnectException ou toute autre exception levée par le SDK (ex: GET_INPUT_EVENT_FAILED)
                Log($"❌ SetInputEvent (hash={hash}): {ex.Message}");
            }
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
    // Quand un nouveau profil est chargé, on doit:
    // 1. Mapper chaque SimEvent du profil à un ID numérique (RegisterProfileEvents)
    // 2. Définir chaque SimVar et s'abonner aux changements (RegisterProfileSimVars)
    // ========================================================================

    /// <summary>
    /// Enregistre tous les K:Events du profil dans SimConnect
    ///
    /// SimConnect nécessite de "mapper" un nom d'event (string) à un ID numérique
    /// avant de pouvoir l'utiliser. Cette méthode crée ce mapping pour toutes
    /// les commandes du profil.
    /// </summary>
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

    /// <summary>
    /// Enregistre toutes les SimVars du profil pour lecture continue
    ///
    /// Pour chaque SimVar:
    /// 1. Créer une définition (AddToDataDefinition) avec le nom et l'unité
    /// 2. Enregistrer la structure de données (RegisterDataDefineStruct)
    /// 3. Demander les mises à jour automatiques (RequestDataOnSimObject)
    ///
    /// La période VISUAL_FRAME + flag CHANGED = notification uniquement quand la valeur change
    /// </summary>
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
    // Ces méthodes sont appelées automatiquement par SimConnect quand:
    // - OnRecvOpen: La connexion est établie
    // - OnRecvQuit: MSFS se ferme
    // - OnRecvException: Une erreur survient (event invalide, etc.)
    // - OnRecvSimobjectData: Des données SimVar sont reçues
    // ========================================================================

    /// <summary>
    /// Callback: Connexion SimConnect établie
    /// </summary>
    private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        Log($"   → MSFS: {data.szApplicationName}");
    }

    /// <summary>
    /// Callback: MSFS se ferme ou connexion perdue
    /// </summary>
    private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Log("⚠️ MSFS fermé");
        Disconnect();
    }

    /// <summary>
    /// Callback: Erreur SimConnect
    /// Les codes d'erreur courants sont traduits en messages lisibles
    /// </summary>
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

    /// <summary>
    /// Callback: Réception de données SimVar
    ///
    /// Cette méthode gère deux types de données:
    /// 1. AIRCRAFT_TITLE_REQUEST: Le titre de l'avion (pour auto-détection du profil)
    /// 2. Autres: Les valeurs SimVar des commandes (lumières, AP, etc.)
    ///
    /// Quand une valeur change, l'événement StateChanged est déclenché
    /// pour notifier l'interface web via WebSocket
    /// </summary>
    private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        // === CAS 1: Titre de l'avion ===
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

        // === CAS 2: Données SimVar d'une commande ===
        // On retrouve le commandId via le mapping _simVarDefinitions
        int requestId = (int)data.dwRequestID;
        if (_simVarDefinitions.TryGetValue(requestId, out var commandId))
        {
            var simVarData = (SimVarData)data.dwData[0];

            lock (_stateLock)
            {
                var oldValue = _buttonStates.GetValueOrDefault(commandId);
                // Seuil de 0.001 pour éviter les notifications dues aux erreurs d'arrondi float
                if (Math.Abs(oldValue - simVarData.Value) > 0.001)
                {
                    _buttonStates[commandId] = simVarData.Value;
                    StateChanged?.Invoke(commandId, simVarData.Value);  // Notifier l'interface web
                }
            }
        }
    }

    /// <summary>
    /// Affiche un message dans la console et notifie les abonnés
    /// </summary>
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

// ============================================================================
// ENUMS SIMCONNECT
// ============================================================================
// SimConnect utilise des enums typés pour les IDs. Ces enums vides permettent
// de caster des int vers le type attendu par l'API (ex: (DefineId)100)
// ============================================================================

enum DefineId { }           // IDs pour les définitions de données (SimVars)
enum RequestId { }          // IDs pour les requêtes de données
enum EventId { }            // IDs pour les événements (K:Events)
enum NotificationGroup { Group0 = 0 }  // Groupe de priorité pour les events
