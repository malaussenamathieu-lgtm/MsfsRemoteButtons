# MsfsRemoteButtons - Documentation de mise en conformité MSFS 2024 SimConnect SDK

**Date**: 4 février 2026  
**Version SDK**: MSFS 2024  
**Projet**: MsfsRemoteButtons (Interface web de contrôle MSFS)

---

## 📋 Résumé exécutif

**État actuel**: Architecture globalement conforme, mais **violation critique du thread-safety** SimConnect.

**Priorité #1**: Implémenter lock global pour protéger tous les appels SimConnect.

**Documentation lue**: General Functions, Input Events, SimConnect Actions

---

## 🔴 CRITIQUE - Thread Safety (URGENT)

### Problème identifié

SimConnect **n'est pas thread-safe** selon Microsoft. Votre application appelle SimConnect depuis 3 threads différents simultanément:

1. **Thread ReceiveLoop** (Program.cs) → appelle `ReceiveMessages()` toutes les 10ms
2. **Thread Main Console** (Program.cs) → appelle `Connect()`, `Disconnect()`, `RequestAircraftTitle()`
3. **Threads EmbedIO WebServer** → appellent `SendCommand()` via WebServerService

**Risque**: Crashes aléatoires, comportement imprévisible, corruption mémoire.

### Actions requises

**SimConnectService.cs**:
- [ ] Ajouter `private readonly object _simConnectLock = new();`
- [ ] Protéger TOUTES les méthodes publiques avec `lock (_simConnectLock)`:
  - `Connect()`
  - `Disconnect()`
  - `ReceiveMessages()` ← **CRUCIAL**
  - `RequestAircraftTitle()`
  - `SetProfile()`
  - `SendCommand()`
  - `RefreshSimVarForCommand()`
  - `SendEventByName()`

**Documentation à ajouter**:
```csharp
// THREAD-SAFETY: All public methods protected by _simConnectLock
// SimConnect is NOT thread-safe (Microsoft documentation)
// Called from: Main thread (console), ReceiveLoop thread, EmbedIO threads
private readonly object _simConnectLock = new();

Référence: SimConnect SDK page "SimConnect_SDK.htm"

🟡 IMPORTANT - Event Dispatch Asynchrone
Problème identifié
Callbacks SimConnect (OnRecvSimobjectData, etc.) invoquent directement events C# (StateChanged?.Invoke()). Si WebServerService fait des opérations longues dans ces handlers, le thread SimConnect est bloqué.

Actions recommandées
SimConnectService.cs - Wrapper les invocations d'events:

 Task.Run(() => StateChanged?.Invoke(commandId, value));

 Task.Run(() => ConnectionChanged?.Invoke(true));

 Task.Run(() => AircraftChanged?.Invoke(CurrentAircraftTitle));

 Task.Run(() => LogMessage?.Invoke(message));

Documentation à ajouter:
// EVENT DISPATCH: Events dispatched asynchronously to avoid blocking SimConnect thread
// SimConnect requires frequent ReceiveMessage() calls - callbacks must be fast
Référence: SimConnect_CallDispatch page

✅ Points conformes validés
Architecture générale
 Application .exe out-of-process (recommandé par Microsoft)

 Langage C#/.NET managed code (supporté officiellement)

 Cas d'usage valide: "Enable new hardware to work with MSFS"

SimConnect_Open
 Nom application descriptif: "MSFS Remote Buttons"

 Window handle IntPtr.Zero (correct pour console)

 WM_USER_SIMCONNECT = 0x0402 (valeur standard)

 ConfigIndex = 0 (connexion locale par défaut)

Callbacks
 OnRecvOpen - Connexion confirmée

 OnRecvQuit - Détection fermeture MSFS

 OnRecvException - Gestion erreurs

 OnRecvSimobjectData - Réception SimVars

ReceiveLoop architecture
 Thread dédié IsBackground = true

 Polling 100 Hz (10ms sleep) - conforme

 While loop comme exemple documentation

 ReceiveMessage() appelé régulièrement

SimConnect_Close
 Dispose() appelé (équivalent .NET de Close)

 Clear() dictionnaires pour cleanup mémoire

 _simConnect = null après fermeture

Gestion IDs
 Compteurs séparés: _nextEventId = 1, _nextDefinitionId = 100

 Clear() des dictionnaires à la déconnexion

 Pas de réutilisation d'IDs pendant session

🟢 Améliorations recommandées
1. Gestion des erreurs
OnRecvException - Ajouter tous les codes:

 CODE 1: ERROR

 CODE 2: SIZE_MISMATCH

 CODE 3: INVALID_DATA_TYPE

 CODE 4: INVALID_DATA_SIZE

 CODE 5: DATA_ERROR

 CODE 6: INVALID_ARRAY

 CODE 7: UNRECOGNIZED_ID (déjà géré)

 CODE 8: UNDEFINED_ID (déjà géré)

 CODE 9: OUT_OF_BOUNDS

 CODE 10: INVALID_DATA_TYPE (déjà géré)

 CODE 11: ALREADY_CREATED

 CODE 12: OPERATION_INVALID_FOR_OBJECT_TYPE

Connect() - Améliorer gestion HRESULT:

 0x80004005 (E_FAIL) → "MSFS n'est pas lancé"

 0x80070057 (E_INVALIDARG) → "ConfigIndex invalide"

ReceiveLoop - Améliorer catch:

 Logger exception au lieu de catch vide

 Thread.Sleep(1000) avant retry si erreur

2. Optimisations performances
RequestDataOnSimObject:

 Évaluer PERIOD.VISUAL_FRAME (60Hz) vs PERIOD.SECOND (1Hz)

 VISUAL_FRAME adapté pour états changeant rapidement

 SECOND suffisant pour lumières, fuel pump (états ON/OFF)

 Tester impact performance avec 20+ SimVars

Benchmark:

 Mesurer nombre de messages SimConnect/seconde

 Identifier goulots d'étranglement

 Optimiser fréquence polling si nécessaire

3. Gestion IDs - Sécurité
Risque collision théorique:

 Si _nextEventId atteint 100 → collision avec _nextDefinitionId

 Ajouter assertion/warning si _nextEventId >= 100

 Alternative: changer _nextDefinitionId = 10000 (safer)

4. OnRecvOpen - Ordre initialisation
Optimisation mineure:

 Déplacer _isConnected = true dans OnRecvOpen (confirmation réelle)

 Actuellement: _isConnected = true avant confirmation (risque théorique)

 Logger version SimConnect: data.dwSimConnectVersionMajor/Minor

🔵 Fonctionnalités futures - Input Events (B:)
Migration K: Events → B: Events
Contexte: MSFS 2024 introduit Input Events (B:) qui remplacent progressivement Key Events (K:).

Avantages B: Events:

Liste dynamique par avion (EnumerateInputEvents)

Notifications natives automatiques (SubscribeInputEvent)

Pas besoin de SimVars séparées pour feedback

Hash = 0 pour monitorer TOUS les events (debug puissant)

Fonctions à implémenter
1. SimConnect_EnumerateInputEvents ⭐
Priorité: MOYENNE-HAUTE

 Appeler après détection avion (dans OnRecvSimobjectData AIRCRAFT_TITLE)

 Créer callback OnRecvEnumerateInputEvents

 Stocker mapping: Dictionary<string, ulong> _inputEventHashes

 Exporter JSON: aircraft_events_{aircraftTitle}.json

 Afficher dans interface web: "B: events available"

Bénéfice: Répond à votre question initiale - liste complète des events par avion !

2. SimConnect_SetInputEvent
Priorité: MOYENNE

 Équivalent de TransmitClientEvent pour B: events

 Créer SendInputEvent(ulong hash, double value)

 Adapter SendCommand pour supporter B: + K: (backward compat)

 Ajouter inputEventHash dans AircraftCommand (optionnel)

 Si hash présent → SetInputEvent, sinon → TransmitClientEvent

3. SimConnect_SubscribeInputEvent
Priorité: MOYENNE

 Équivalent de RequestDataOnSimObject pour B: events

 Créer callback OnRecvSubscribeInputEvent

 S'abonner lors chargement profil (comme RegisterProfileSimVars)

 Mettre à jour _buttonStates dans callback

 Feature debug: SubscribeInputEvent(0) pour monitorer TOUS les events

4. SimConnect_GetInputEvent
Priorité: BASSE

 Lecture ponctuelle d'un B: event

 Créer callback OnRecvGetInputEvent

 Alternative à Subscribe si pas besoin de notifications continues

5. SimConnect_UnsubscribeInputEvent
Priorité: BASSE

 Appeler dans Disconnect() avec hash=0

 Appeler lors changement profil (cleanup)

 Pas critique (SimConnect cleanup auto à la fermeture)

6. SimConnect_EnumerateInputEventParams
Priorité: TRÈS BASSE

 Liste paramètres requis par un B: event (";FLOAT64", etc.)

 Complexe: packing byte array

 La plupart des events simples n'ont pas de paramètres

 À faire après EnumerateInputEvents de base

Workflow complet B: Events
1. Chargement avion
   ↓
2. EnumerateInputEvents → liste hash + noms
   ↓
3. Export JSON (documentation auto)
   ↓
4. SubscribeInputEvent(hash) → notifications auto
   ↓
5. SetInputEvent(hash, value) → envoyer commande
   ↓
6. OnRecvSubscribeInputEvent → recevoir feedback
Format JSON suggéré
{
  "aircraftTitle": "Cessna 172 Skyhawk G1000",
  "inputEvents": [
    {
      "name": "NAV_LIGHTS_TOGGLE",
      "hash": "12345678901234567",
      "params": ""
    },
    {
      "name": "AS1000_MID_COM_1_Mic_Position",
      "hash": "11675888408130357189",
      "params": ";FLOAT64"
    }
  ]
}

🟢 Fonctionnalités optionnelles
SimConnect_RequestSystemState
Priorité: BASSE

 Alternative à TITLE SimVar pour détecter avion

 États disponibles: AircraftLoaded, DialogMode, FlightLoaded, FlightPlan, Sim

 Cas d'usage: Désactiver commandes si DialogMode = true (dans menus)

 Implémenter callback OnRecvSystemState

SimConnect_ExecuteAction
Priorité: BASSE

 Appeler actions XML avec paramètres

 FocusInstrumentAction: Highlight instrument quand bouton web cliqué

 Feature UX: Montrer quel switch actionner dans cockpit

 Complexe: empaqueter paramètres en byte array

 Lire liste complète: SimConnect Actions (page WIP)

SimConnect_SetNotificationGroupPriority
Priorité: TRÈS BASSE

 Utile si plusieurs clients SimConnect contrôlent même avion

 Actuellement: une seule application = pas nécessaire

 Flag GROUPID_IS_PRIORITY actuel semble suffisant