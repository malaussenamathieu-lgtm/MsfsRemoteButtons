using Microsoft.FlightSimulator.SimConnect;
using MsfsRemoteButtons.Profiles;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection;

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

    // B: EVENT: ID de requête pour l'énumération des Input Events (EnumerateInputEvents)
    private const int INPUT_EVENTS_REQUEST_ID = 9999;

    // === ÉTATS DES CONTRÔLES ===
    // Cache local des valeurs SimVar pour éviter les requêtes répétées
    private readonly Dictionary<string, double> _buttonStates = new();      // commandId -> valeur actuelle (0.0 ou 1.0 pour Bool)
    private readonly object _stateLock = new();                             // Verrou pour accès thread-safe

    // THREAD-SAFETY: SimConnect n'est pas thread-safe (documentation Microsoft). Tous les appels _simConnect.* doivent être dans lock (_simConnectLock).
    private readonly object _simConnectLock = new();

    // B: EVENT: Stockage des Input Events énumérés (nom → hash). Rempli par OnRecvEnumerateInputEvents.
    private readonly Dictionary<string, ulong> _inputEventHashes = new();
    private readonly object _inputEventLock = new();
    private bool _inputEventsEnumerated;
    // null = pas encore testé, true = Developer Mode actif, false = inactif ou timeout
    private bool? _developerModeDetected;

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

    /// <summary>True si l'énumération des B: Input Events a été effectuée (Developer Mode requis).</summary>
    // B: EVENT
    public bool InputEventsEnumerated => _inputEventsEnumerated;

    /// <summary>True si Developer Mode est actif dans MSFS (B: events disponibles).</summary>
    // B: EVENT
    public bool IsDeveloperModeActive => _developerModeDetected == true;

    // Réflexion: handle natif SimConnect (pour ExecuteCalculatorCode des LocalVars)
    private static readonly FieldInfo? _simConnectHandleField =
        typeof(SimConnect).GetField("hSimConnect", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Tente de se connecter à MSFS avec retry automatique (3 tentatives, délais 0 / 2 s / 5 s).
    /// </summary>
    public bool Connect()
    {
        if (_isConnected) return true;

        const int maxAttempts = 3;
        int[] retryDelaysMs = { 0, 2000, 5000 }; // 0 ms avant 1ère, 2 s avant 2e, 5 s avant 3e

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Log($"Tentative de connexion {attempt}/{maxAttempts}...");

            if (attempt > 1)
            {
                int waitMs = retryDelaysMs[attempt - 1];
                int waitSec = waitMs / 1000;
                Log($"   Attente de {waitSec} s avant la prochaine tentative...");
                for (int s = waitSec; s > 0; s--)
                {
                    Thread.Sleep(1000);
                    Log($"   {s} s...");
                }
            }

            if (AttemptConnection())
                return true;
        }

        Log("❌ Échec après 3 tentatives. Connexion impossible.");
        Log("   → Vérifiez que MSFS 2024 est lancé et entièrement chargé (écran de vol ou menu).");
        Log("   → Vous pouvez réessayer plus tard avec la touche [C].");
        return false;
    }

    /// <summary>
    /// Effectue une tentative de connexion SimConnect (création, attente RECV_OPEN, gestion erreurs).
    /// </summary>
    private bool AttemptConnection()
    {
        try
        {
            Log("   Connexion à Microsoft Flight Simulator 2024 en cours...");

            lock (_simConnectLock)
            {
                _simConnect = new SimConnect("MSFS Remote Buttons", IntPtr.Zero, WM_USER_SIMCONNECT, null, 0);

                _simConnect.OnRecvOpen += OnRecvOpen;
                _simConnect.OnRecvQuit += OnRecvQuit;
                _simConnect.OnRecvException += OnRecvException;
                _simConnect.OnRecvSimobjectData += OnRecvSimobjectData;
                _simConnect.OnRecvEnumerateInputEvents += OnRecvEnumerateInputEvents;

                _simConnect.AddToDataDefinition(
                    (DefineId)AIRCRAFT_TITLE_DEFINITION,
                    "TITLE",
                    null,
                    SIMCONNECT_DATATYPE.STRING256,
                    0,
                    SimConnect.SIMCONNECT_UNUSED
                );
                _simConnect.RegisterDataDefineStruct<AircraftTitleData>((DefineId)AIRCRAFT_TITLE_DEFINITION);
            }

            Log("   En attente de la confirmation du simulateur (max. 10 s)...");

            var timeout = TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;

            while (!_isConnected && (DateTime.UtcNow - startTime) < timeout)
            {
                lock (_simConnectLock)
                {
                    _simConnect?.ReceiveMessage();
                }
                Thread.Sleep(10);
            }

            if (!_isConnected)
            {
                Log("   ❌ Le simulateur n'a pas répondu à temps.");
                Log("   → Vérifiez que MSFS 2024 est bien lancé et entièrement chargé.");
                lock (_simConnectLock)
                {
                    _simConnect?.Dispose();
                    _simConnect = null;
                }
                return false;
            }

            return true;
        }
        catch (COMException ex)
        {
            int hr = ex.HResult;
            Log($"   ❌ Erreur (code 0x{hr:X8}) : {ex.Message}");

            if (hr == unchecked((int)0x80004005))
                Log("   → MSFS 2024 n'est probablement pas lancé. Lancez-le puis réessayez.");
            else if (hr == unchecked((int)0x80070005))
                Log("   → Accès refusé. Essayez « Exécuter en tant qu'administrateur ».");
            else
                Log("   → Vérifiez que MSFS 2024 est installé et à jour.");

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
            lock (_simConnectLock)
            {
                _simConnect.Dispose();
                _simConnect = null;
            }
        }
        _isConnected = false;
        _eventIds.Clear();
        _simVarDefinitions.Clear();
        _buttonStates.Clear();
        lock (_inputEventLock)
        {
            _inputEventHashes.Clear();
        }
        _inputEventsEnumerated = false;
        _developerModeDetected = null;
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
            lock (_simConnectLock)
            {
                _simConnect.ReceiveMessage();
            }
        }
        catch (Exception)
        {
            // Connexion perdue
            Disconnect();
        }
    }

    /// <summary>
    /// Récupère le hash d'un Input Event (B:) par son nom, si l'énumération a été faite.
    /// </summary>
    /// <param name="name">Nom de l'event (ex: "NAV_LIGHTS_SET").</param>
    /// <param name="hash">Hash reçu si trouvé.</param>
    /// <returns>True si le hash a été trouvé.</returns>
    // B: EVENT
    public bool TryGetInputEventHash(string name, out ulong hash)
    {
        lock (_inputEventLock)
        {
            return _inputEventHashes.TryGetValue(name, out hash);
        }
    }

    /// <summary>
    /// Demande le titre de l'avion actuel
    /// </summary>
    public void RequestAircraftTitle()
    {
        if (_simConnect == null || !_isConnected) return;

        lock (_simConnectLock)
        {
            _simConnect.RequestDataOnSimObject(
            (RequestId)AIRCRAFT_TITLE_REQUEST,
            (DefineId)AIRCRAFT_TITLE_DEFINITION,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.ONCE,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0, 0, 0
            );
        }
    }

    /// <summary>
    /// Demande l'énumération des Input Events (B:) disponibles. Les résultats arrivent dans OnRecvEnumerateInputEvents.
    /// Nécessite Developer Mode activé dans MSFS. Seuls les events _SET sont retournés.
    /// </summary>
    // B: EVENT
    public void EnumerateInputEvents()
    {
        if (_simConnect == null || !_isConnected) return;

        lock (_simConnectLock)
        {
            Log("B: EVENT: Demande d'énumération des Input Events...");
            _simConnect.EnumerateInputEvents((RequestId)INPUT_EVENTS_REQUEST_ID);
        }

        // Timeout: si aucun retour sous 5 s, Developer Mode est probablement désactivé
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);
            if (_developerModeDetected != null) return; // Callback déjà passé
            _developerModeDetected = false;
            Log("⚠️ Aucune réponse MSFS pour les Input Events (timeout 5 s).");
            Log("   Developer Mode est peut-être désactivé : Options → General → Developers → Developer Mode ON");
            Log("   Redémarrez MSFS après activation. Les K: events (legacy) restent utilisables.");
        });
    }

    /// <summary>
    /// Change le profil actif et enregistre les SimVars/Events
    /// </summary>
    public void SetProfile(IAircraftProfile profile)
    {
        _activeProfile = profile;
        RegisterProfileEvents();
        RegisterProfileSimVars();

        // B: EVENT: Énumérer les Input Events disponibles (nécessite Developer Mode dans MSFS)
        Log("🔍 Énumération des Input Events disponibles...");
        Log("   Commandes : K: events (toujours disponibles). B: events si Developer Mode activé dans MSFS.");
        EnumerateInputEvents();

        // LocalVars : initialiser l'état pour les commandes sans SimVar (avec protection)
        // NOTE: Désactivé temporairement au chargement pour éviter les crashes SimConnect
        // Les LocalVars seront lues à la demande via RefreshSimVarForCommand
        // foreach (var cmd in _activeProfile.Commands)
        // {
        //     if (string.IsNullOrEmpty(cmd.SimVar) && !string.IsNullOrEmpty(cmd.LocalVar))
        //     {
        //         try
        //         {
        //             RefreshLocalVarState(cmd.Id, cmd.LocalVar, cmd.LocalVarUnit);
        //         }
        //         catch (Exception ex)
        //         {
        //             Log($"⚠️ Erreur lecture LVar {cmd.LocalVar}: {ex.Message}");
        //         }
        //     }
        // }

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

        // B: EVENT: si la commande a un Input Event et qu'on a le hash, utiliser SetInputEvent (prioritaire sur K:)
        if (repeatCount == 1 && TryResolveInputEventHash(command, out ulong inputHash))
        {
            SendInputEventCommand(actualCommandId, command, inputHash);
            return;
        }

        // Exécution K: events (legacy) avec délai entre chaque répétition
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
    /// Résout le hash B: d'une commande (propriété InputEventHash ou lookup par InputEvent).
    /// </summary>
    // B: EVENT
    private bool TryResolveInputEventHash(AircraftCommand command, out ulong hash)
    {
        hash = 0;
        if (command.InputEventHash.HasValue && command.InputEventHash.Value != 0)
        {
            hash = command.InputEventHash.Value;
            return true;
        }
        if (!string.IsNullOrEmpty(command.InputEvent) && TryGetInputEventHash(command.InputEvent, out hash))
            return true;
        return false;
    }

    /// <summary>
    /// Envoie une commande via B: Input Event (SetInputEvent).
    /// AS1000 autopilot: toujours value=1 (MSFS gère le toggle). Autres: lit l'état, inverse.
    /// </summary>
    // B: EVENT
    private void SendInputEventCommand(string commandId, AircraftCommand command, ulong hash)
    {
        string? eventName = command.InputEvent;
        bool isToggleOnly = commandId.StartsWith("ap_", StringComparison.OrdinalIgnoreCase) ||
                           (eventName?.Contains("AS1000_AUTOPILOT", StringComparison.OrdinalIgnoreCase) ?? false);
        bool isBreakerEvent = eventName?.Contains("BREAKER", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isToggleOnly)
        {
            SetInputEvent(hash, 1.0);
            Log($"→ {commandId} (B: EVENT toggle: {eventName} = 1)");
        }
        else if (isBreakerEvent)
        {
            // Breaker events: 0 = ON, 1 = OFF (inverse de la logique normale)
            double currentState = GetState(commandId);
            double newValue = currentState > 0.5 ? 1.0 : 0.0; // Si ON → envoyer 1 (OFF), si OFF → envoyer 0 (ON)
            SetInputEvent(hash, newValue);
            Log($"→ {commandId} (B: BREAKER {eventName} = {newValue}, état={currentState})");
        }
        else
        {
            double currentState = GetState(commandId);
            double newValue = currentState > 0.5 ? 0.0 : 1.0;
            SetInputEvent(hash, newValue);
            Log($"→ {commandId} (B: {eventName} = {newValue})");
        }

        RefreshSimVarForCommand(commandId);
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

                lock (_simConnectLock)
                {
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
        if (command == null) return;

        // Cas SimVar classique
        if (!string.IsNullOrEmpty(command.SimVar))
        {
            try
            {
                // Chercher la defId associée à cette command
                var defId = _simVarDefinitions.FirstOrDefault(x => x.Value == commandId).Key;
                if (defId > 0)
                {
                    // Re-demander les données immédiatement
                    lock (_simConnectLock)
                    {
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
            }
            catch (Exception ex)
            {
                Log($"⚠️ Erreur refresh SimVar pour {commandId}: {ex.Message}");
            }
            return;
        }

        // Cas LocalVar : lecture directe (fallback lorsque SimVar absente)
        if (!string.IsNullOrEmpty(command.LocalVar))
        {
            RefreshLocalVarState(commandId, command.LocalVar, command.LocalVarUnit);
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
            lock (_simConnectLock)
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
                lock (_simConnectLock)
                {
                    int eventId = _nextEventId++;
                    _eventIds[command.Id] = eventId;
                    _simConnect.MapClientEventToSimEvent((EventId)eventId, command.SimEvent);
                }
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
                lock (_simConnectLock)
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
    /// Callback: Connexion SimConnect confirmée par MSFS (RECV_OPEN).
    /// C'est ici que l'état connecté est établi après la réponse du simulateur.
    /// </summary>
    private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        _isConnected = true;
        Log("✅ Connexion réussie à Microsoft Flight Simulator 2024.");
        Log($"   Application : {data.szApplicationName}");
        Log($"   Version MSFS : {data.dwApplicationVersionMajor}.{data.dwApplicationVersionMinor} (build {data.dwApplicationBuildMajor}.{data.dwApplicationBuildMinor})");
        Log($"   Version SimConnect : {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}");

        Task.Run(() => ConnectionChanged?.Invoke(true));

        // Demander le titre de l'avion une fois la connexion confirmée
        RequestAircraftTitle();
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
    /// Callback: Réception de la liste des Input Events (B:) énumérés.
    /// Peut être appelé en plusieurs paquets (pagination). Ne pas bloquer le thread SimConnect → Task.Run.
    /// </summary>
    // B: EVENT
    // EVENT DISPATCH: Task.Run() pour ne pas bloquer le thread SimConnect
    private void OnRecvEnumerateInputEvents(SimConnect sender, SIMCONNECT_RECV_ENUMERATE_INPUT_EVENTS data)
    {
        Task.Run(() =>
        {
            try
            {
                int count = data.rgData?.Length ?? 0;
                Log($"B: EVENT: {count} Input Event(s) reçu(s)");

                if (count == 0)
                {
                    _developerModeDetected = false;
                    Log("⚠️ Developer Mode semble désactivé");
                    Log("   Les Input Events (B: events) ne sont pas disponibles");
                    Log("   → Pour activer : Options → General → Developers → Developer Mode ON");
                    Log("   → Redémarrez MSFS après activation");
                    Log("   ℹ️ Les K: events (legacy) restent fonctionnels");
                    return;
                }

                _developerModeDetected = true;
                Log("✅ Developer Mode actif - B: events disponibles");

                // Premier paquet : vider le cache. Les paquets suivants s'ajoutent.
                if (data.dwEntryNumber == 0)
                {
                    lock (_inputEventLock)
                    {
                        _inputEventHashes.Clear();
                    }
                }

                lock (_inputEventLock)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var item = data.rgData![i] as SIMCONNECT_INPUT_EVENT_DESCRIPTOR;
                        if (item == null) continue;
                        string name = item.Name ?? "";
                        if (string.IsNullOrEmpty(name)) continue;
                        ulong hash = (ulong)item.Hash;
                        _inputEventHashes[name] = hash;
#if DEBUG
                        Log($"   B: {name} = {hash}");
#endif
                    }
                }

                // Dernier paquet de la liste
                if (data.dwEntryNumber >= data.dwOutOf - 1)
                {
                    _inputEventsEnumerated = true;
                    int total;
                    lock (_inputEventLock)
                    {
                        total = _inputEventHashes.Count;
                        Log($"✅ {total} Input Event(s) catalogué(s)");
                    }
                    if (total > 0)
                        ExportInputEventsToFile();
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Erreur traitement Input Events: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Exporte la liste des B: Input Events vers un fichier texte (documentation / debug).
    /// Fichier : {AircraftName}_InputEvents.txt dans le répertoire courant (Directory.GetCurrentDirectory()).
    /// </summary>
    // B: EVENT
    private void ExportInputEventsToFile()
    {
        string aircraftName;
        List<KeyValuePair<string, ulong>> snapshot;
        lock (_inputEventLock)
        {
            if (_inputEventHashes.Count == 0) return;
            aircraftName = _activeProfile?.AircraftName ?? "Unknown";
            snapshot = _inputEventHashes.OrderBy(kv => kv.Key).ToList();
        }

        string safeName = string.Join("_", aircraftName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrEmpty(safeName)) safeName = "Aircraft";
        string directory = Directory.GetCurrentDirectory();
        string filePath = Path.Combine(directory, $"{safeName}_InputEvents.txt");

        var lines = new List<string>
        {
            "=================================================================",
            "MSFS 2024 - Input Events (B: events)",
            "=================================================================",
            $"Aircraft: {aircraftName}",
            $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Total Events: {snapshot.Count}",
            "Developer Mode: Required (must be ON in MSFS)",
            "",
            "Note: Only _SET type events are enumerated. _TOGGLE, _INC, _DEC variants exist but are not listed.",
            ""
        };

        foreach (var kv in snapshot)
            lines.Add($"{kv.Key} = 0x{kv.Value:X8} ({kv.Value})");

        lines.Add("");
        lines.Add("=================================================================");

        try
        {
            if (!Directory.Exists(directory))
                return;
            File.WriteAllLines(filePath, lines, System.Text.Encoding.UTF8);
            string fullPath = Path.GetFullPath(filePath);
            Log($"📄 Input Events exportés : {fullPath} ({snapshot.Count} event(s))");
        }
        catch (Exception ex)
        {
            Log($"⚠️ Erreur export Input Events : {ex.Message}");
        }
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

                Task.Run(() => AircraftChanged?.Invoke(CurrentAircraftTitle));
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
                    Task.Run(() => StateChanged?.Invoke(commandId, simVarData.Value));  // Notifier l'interface web sans bloquer SimConnect
                }
            }
        }
    }

    /// <summary>
    /// Lit une LocalVariable (L: var) via l'API native ExecuteCalculatorCode.
    /// </summary>
    private bool TryReadLocalVar(string localVar, string? unit, out double value)
    {
        value = 0;
        if (_simConnect == null || !_isConnected) return false;
        
        try
        {
            var handle = GetSimConnectHandle();
            if (handle == IntPtr.Zero)
            {
                Log($"⚠️ LVar: handle SimConnect non disponible");
                return false;
            }

            string safeUnit = string.IsNullOrWhiteSpace(unit) ? "Number" : unit!;
            string code = $"(L:{localVar},{safeUnit})";

            double result = 0;
            int hr = unchecked((int)0x80004005); // E_FAIL par défaut
            
            // Vérifier que le handle est valide avant l'appel
            if (handle == IntPtr.Zero)
            {
                return false;
            }
            
            try
            {
                lock (_simConnectLock)
                {
                    hr = NativeSimConnect.ExecuteCalculatorCode(handle, code, ref result, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch (DllNotFoundException)
            {
                Log($"⚠️ SimConnect.dll non trouvé pour ExecuteCalculatorCode");
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                Log($"⚠️ ExecuteCalculatorCode non disponible dans SimConnect.dll (MSFS 2024?)");
                return false;
            }
            catch (Exception ex)
            {
                Log($"⚠️ Exception ExecuteCalculatorCode: {ex.GetType().Name} - {ex.Message}");
                return false;
            }

            if (hr == 0)
            {
                value = result;
                return true;
            }

            // Ne pas logger les erreurs HRESULT normales (LVar peut ne pas exister)
            if (hr != unchecked((int)0x80004005)) // E_FAIL
            {
                Log($"⚠️ LVar read failed: {localVar} hr=0x{hr:X8}");
            }
            return false;
        }
        catch (Exception ex)
        {
            Log($"⚠️ Exception LVar {localVar}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Retourne le handle natif SimConnect (pour les appels P/Invoke).
    /// </summary>
    private IntPtr GetSimConnectHandle()
    {
        if (_simConnect == null || _simConnectHandleField == null) return IntPtr.Zero;
        var val = _simConnectHandleField.GetValue(_simConnect);
        return val is IntPtr ptr ? ptr : IntPtr.Zero;
    }

    /// <summary>
    /// Met à jour le cache d'état à partir d'une LocalVariable et notifie si changement.
    /// </summary>
    private void RefreshLocalVarState(string commandId, string localVar, string? unit)
    {
        try
        {
            if (TryReadLocalVar(localVar, unit, out var newValue))
            {
                lock (_stateLock)
                {
                    var oldValue = _buttonStates.GetValueOrDefault(commandId);
                    if (Math.Abs(oldValue - newValue) > 0.001)
                    {
                        _buttonStates[commandId] = newValue;
                        Task.Run(() => StateChanged?.Invoke(commandId, newValue));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"⚠️ Erreur RefreshLocalVarState {commandId}: {ex.Message}");
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

internal static class NativeSimConnect
{
    // Signature native SimConnect_ExecuteCalculatorCode (non exposée dans le wrapper managed)
    // NOTE: Cette fonction peut ne pas exister dans MSFS 2024 SimConnect.dll
    // Si elle cause des crashes, désactiver les appels LocalVar
    [DllImport("SimConnect.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    public static extern int ExecuteCalculatorCode(
        IntPtr hSimConnect,
        [MarshalAs(UnmanagedType.LPStr)] string szCode,
        ref double value,
        IntPtr reserved1,
        IntPtr reserved2);
}
