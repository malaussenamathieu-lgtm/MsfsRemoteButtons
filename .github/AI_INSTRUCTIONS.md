# Instructions pour IA - MsfsRemoteButtons

**Projet**: Interface web de contrôle Microsoft Flight Simulator 2024 via SimConnect  
**Langage**: C# / .NET 8.0  
**Architecture**: Application console + serveur web EmbedIO + SimConnect managed wrapper

---

## ⚠️ RÈGLES CRITIQUES - À RESPECTER ABSOLUMENT

### 🔴 Thread Safety SimConnect

**RÈGLE #1**: SimConnect n'est PAS thread-safe (documentation Microsoft officielle)

**Implémentation OBLIGATOIRE**:
```csharp
// Dans SimConnectService.cs
private readonly object _simConnectLock = new();

public void AnyPublicMethod()
{
    lock (_simConnectLock)
    {
        // TOUS les appels _simConnect ici
    }
}

Méthodes à protéger:

Connect()

Disconnect()

ReceiveMessages() ← CRUCIAL car appelé depuis thread dédié

RequestAircraftTitle()

SetProfile()

SendCommand()

RefreshSimVarForCommand()

SendEventByName()

Pourquoi: 3 threads appellent SimConnect simultanément:

ReceiveLoop thread (Program.cs, toutes les 10ms)

Main Console thread (touches C/D/R)

EmbedIO WebServer threads (requêtes HTTP/WebSocket)

🟡 Event Dispatch Asynchrone

RÈGLE #2: Ne JAMAIS bloquer le thread SimConnect dans les callbacks

Implémentation OBLIGATOIRE:

// Dans les callbacks OnRecv*
private void OnRecvSimobjectData(...)
{
    // Calculs rapides OK ici
    
    // Mais dispatch events en background:
    Task.Run(() => StateChanged?.Invoke(commandId, value));
    Task.Run(() => ConnectionChanged?.Invoke(true));
    Task.Run(() => AircraftChanged?.Invoke(title));
    Task.Run(() => LogMessage?.Invoke(message));
}

Pourquoi: ReceiveMessage() doit être appelé fréquemment. Si callbacks bloquent, la queue SimConnect se remplit et provoque des lags.

📚 Documentation de référence
AVANT toute modification de SimConnectService.cs:

Lire .github/SIMCONNECT_COMPLIANCE.md (conformité complète SDK)

Consulter SDK officiel: https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/SimConnect/

Fichiers critiques du projet:

SimConnectService.cs - Interface SimConnect (thread-safety CRUCIAL)

Program.cs - ReceiveLoop thread

WebServerService.cs - Serveur web EmbedIO

Profiles/*.cs - Profils avions

🎯 Architecture actuelle
Flux de commandes
text
WebSocket Request (thread EmbedIO)
    ↓
WebServerService
    ↓
SimConnectService.SendCommand() [LOCK]
    ↓
SimConnect → MSFS
Flux de données
text
MSFS → SimConnect
    ↓
ReceiveLoop thread appelle ReceiveMessages() [LOCK]
    ↓
Callbacks OnRecv* (thread SimConnect)
    ↓
Task.Run() → Events C#
    ↓
WebServerService → WebSocket (thread pool)
Threads actifs
Main - Console commands

ReceiveLoop - SimConnect polling (10ms, 100Hz)

EmbedIO threads - HTTP/WebSocket requests

Task pool - Event dispatch async

🔧 Conventions de code
Naming
_simConnect - Instance SimConnect (null si déconnecté)

_simConnectLock - Lock pour thread-safety

_eventIds - Mapping commandId → SimConnect eventId

_simVarDefinitions - Mapping defId → commandId

_buttonStates - Cache des valeurs SimVars (protégé par _stateLock)

Commentaires obligatoires
csharp
// THREAD-SAFETY: Protected by _simConnectLock
// EVENT DISPATCH: Task.Run() to avoid blocking SimConnect thread
// SIMCONNECT: Equivalent de [fonction native] en managed code
Gestion erreurs
TOUJOURS logger les exceptions SimConnect

TOUJOURS try/catch autour des appels SimConnect

NE JAMAIS masquer les erreurs (catch vide interdit)

🚀 TODO Priorités
🔴 URGENT (cette semaine)
 Implémenter _simConnectLock dans SimConnectService

 Ajouter lock dans TOUTES les méthodes publiques

 Wrapper tous les events avec Task.Run()

 Tester avec 5+ clients WebSocket simultanés

 Améliorer gestion erreurs OnRecvException (tous les codes)

🟡 IMPORTANT (ce mois)
 Implémenter SimConnect_EnumerateInputEvents (liste B: events)

 Export JSON des Input Events par avion

 Tester SimConnect_SetInputEvent (B: events)

 Optimiser SIMCONNECT_PERIOD (VISUAL_FRAME vs SECOND)

🟢 FUTUR (backlog)
 Migration progressive K: Events → B: Events

 SimConnect_SubscribeInputEvent pour feedback auto

 Feature "Monitor All Events" (hash=0 pour debug)

 Support SimConnect_ExecuteAction (FocusInstrumentAction)

Détails complets: Voir .github/SIMCONNECT_COMPLIANCE.md

❌ À NE JAMAIS FAIRE
❌ Appeler _simConnect.* sans lock (_simConnectLock)

❌ Bloquer le thread SimConnect dans les callbacks (pas de Thread.Sleep, pas de I/O sync)

❌ Réutiliser un ID SimConnect pendant la session

❌ Catch vide qui masque les erreurs

❌ Modifier SimConnectService sans lire la compliance doc

❌ Utiliser SimConnect_MapInputEventToClientEvent (deprecated, utiliser _EX1)

✅ Bonnes pratiques
✅ Lock global pour tous appels SimConnect

✅ Task.Run() pour dispatch events

✅ Logger toutes les exceptions avec contexte

✅ Commenter les sections complexes (RPN, thread-safety, etc.)

✅ Tester avec plusieurs clients simultanés

✅ Documenter les choix d'architecture

🐛 Debug
SimConnect Inspector
Activer DevMode dans MSFS 2024

Tools → SimConnect Inspector

Voir tous les messages en temps réel

Logging
private void Log(string message)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    LogMessage?.Invoke(message);
}
Tests thread-safety
Ouvrir 10 clients web simultanés

Spam buttons pendant 1 minute

Vérifier aucun crash, aucune exception

📞 Contact / Questions
Compliance doc: .github/SIMCONNECT_COMPLIANCE.md

SDK officiel: https://docs.flightsimulator.com/msfs2024/

Projet GitHub: https://github.com/malaussenamathieu-lgtm/MsfsRemoteButtons

Version: 1.0
Date: 4 février 2026
MSFS SDK: 2024