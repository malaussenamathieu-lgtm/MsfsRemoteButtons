# Architecture MsfsRemoteButtons

## Thread Safety
- SimConnect n'est pas thread-safe (Microsoft doc)
- Tous les appels protégés par _simConnectLock
- 3 sources de threads:
  - ReceiveLoop (10ms polling)
  - Main Console (commandes C/D/R)
  - EmbedIO WebServer (requêtes HTTP/WebSocket)

## Event Dispatch
- Callbacks SimConnect appellent Task.Run() pour dispatcher events
- Évite de bloquer thread SimConnect
- WebServerService reçoit events dans background threads

## Flux de données
WebSocket Request → Queue → SimConnectLock → SimConnect → MSFS
MSFS → SimConnect → Callbacks → Task.Run() → StateChanged → WebSocket
Commentaires code
SimConnectService.cs:
// THREAD-SAFETY: All public methods protected by _simConnectLock
// SimConnect is NOT thread-safe (Microsoft documentation)
// Called from: Main thread (console), ReceiveLoop thread, EmbedIO threads
private readonly object _simConnectLock = new();
Program.cs ReceiveLoop:
// Poll SimConnect every 10ms (100 Hz)
// ReceiveMessage() processes queue and triggers callbacks
// Important: Must be called frequently (Microsoft doc)
// Thread-safety: ReceiveMessages() protected by lock in SimConnectService
📚 Documentation SimConnect restante à lire
Sections non encore lues
 Events And Data (section complète)

SimConnect_MapClientEventToSimEvent

SimConnect_TransmitClientEvent (détails)

SimConnect_AddToDataDefinition (détails)

SimConnect_RequestDataOnSimObject (détails)

SimConnect_RegisterDataDefineStruct

 Structures and Enumerations

SIMCONNECT_EXCEPTION (enum complète)

SIMCONNECT_RECV_* (toutes structures)

SIMCONNECT_PERIOD

SIMCONNECT_DATA_REQUEST_FLAG

SIMCONNECT_EVENT_FLAG

 Key Events (K:) - Liste complète

Aircraft Autopilot/Flight Assist Events

Aircraft Electrical Events

Aircraft Engine Events

Aircraft Flight Control Events

Aircraft Fuel System Events

Etc.

 Simulation Variables - Liste complète

Variables disponibles par catégorie

Unités supportées

 Programming SimConnect Clients Using Managed Code

Spécificités C#/.NET

Best practices managed code

Progression
✅ General Functions (~90%)

✅ Input Events (~100%)

✅ SimConnect Actions (~100%)

⏳ Events And Data (0%)

⏳ Structures (10%)

⏳ Key Events (0%)

⏳ Simulation Variables (0%)

✅ Checklist de mise en conformité
Immédiat (cette semaine)
 Implémenter lock global _simConnectLock

 Tester avec 5+ clients WebSocket simultanés

 Wrapper events avec Task.Run()

 Améliorer gestion erreurs OnRecvException

 Améliorer catch ReceiveLoop

Court terme (ce mois)
 Créer ARCHITECTURE.md

 Ajouter commentaires thread-safety dans code

 Implémenter EnumerateInputEvents

 Export JSON des B: events par avion

 Tester SetInputEvent avec un event simple

Moyen terme (futur)
 Migration progressive K: → B: events

 SubscribeInputEvent pour feedback auto

 Feature "Monitor All Events" (debug)

 Optimiser PERIOD (VISUAL_FRAME vs SECOND)

 Feature FocusInstrumentAction (UX)

Long terme (backlog)
 Support EnumerateInputEventParams

 Support B: events avec paramètres complexes

 RequestSystemState pour DialogMode

 Profils dynamiques basés sur EnumerateInputEvents

📖 Références documentation
Pages lues:

SimConnect_SDK.htm

SimConnect_API_Reference.htm

DispatchProc.htm

SimConnect_CallDispatch.htm

SimConnect_Open.htm

SimConnect_Close.htm

SimConnect_GetNextDispatch.htm

SimConnect_RequestSystemState.htm

SimConnect_SetNotificationGroupPriority.htm

SimConnect_ExecuteAction.htm

SimConnect_Actions.htm

SimConnect_EnumerateControllers.htm

SimConnect_EnumerateInputEvents.htm

SimConnect_EnumerateInputEventParams.htm

SimConnect_GetInputEvent.htm

SimConnect_MapInputEventToClientEvent_EX1.htm

SimConnect_SetInputEvent.htm

SimConnect_SubscribeInputEvent.htm

SimConnect_UnsubscribeInputEvent.htm

URL base: https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/SimConnect/

Fin du document - Version 1.0 - 4 février 2026