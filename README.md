# MSFS Remote Buttons

Telecommande web pour Microsoft Flight Simulator 2024. Permet de controler les systemes d'un avion depuis n'importe quel navigateur web sur le reseau local.

---

## GUIDE POUR LES IA - LOGIQUE METIER

Ce document explique l'architecture et la logique metier du projet pour faciliter les modifications futures.

---

## 1. ARCHITECTURE GLOBALE

```
┌─────────────────────────────────────────────────────────────────────┐
│                        MSFS 2024                                    │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │ SimConnect (COM)
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    SimConnectService.cs                             │
│  - Connexion/deconnexion MSFS                                       │
│  - Envoi de commandes (K:Events)                                    │
│  - Lecture d'etat (SimVars)                                         │
│  - Detection automatique de l'avion                                 │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │ Evenements C# (StateChanged, etc.)
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    WebServerService.cs                              │
│  - Serveur HTTP (port 8080)                                         │
│  - WebSocket bidirectionnel                                         │
│  - Broadcast des etats aux clients                                  │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │ WebSocket JSON
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Web/index.html                                   │
│  - Interface utilisateur dynamique                                  │
│  - Generation des boutons selon le profil                           │
│  - Feedback visuel (LEDs)                                           │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 2. SYSTEME DE PROFILS

### 2.1 Structure d'un profil (`IAircraftProfile`)

Chaque avion a un **profil** qui definit ses commandes disponibles.

```
Profiles/
├── IAircraftProfile.cs          # Interface et classes de base
├── ProfileManager.cs            # Detection auto du profil
└── Cessna172G1000Profile.cs     # Implementation Cessna 172
```

### 2.2 Classes de base (`IAircraftProfile.cs`)

| Classe | Role |
|--------|------|
| `ControlType` | Enum: `Toggle`, `Selector`, `Momentary` |
| `SelectorOption` | Option d'un selecteur (Label, SimEvent, Value) |
| `AircraftCommand` | Definition complete d'une commande |
| `AircraftPattern` | Pattern pour detection auto de l'avion |
| `IAircraftProfile` | Interface du profil |

### 2.3 Structure d'une commande (`AircraftCommand`)

```csharp
new AircraftCommand
{
    // === IDENTIFICATION ===
    Id = "nav_lights",           // Identifiant UNIQUE (utilise partout)
    Name = "Nav",                // Nom affiche dans l'interface
    Category = "LUMIERES",       // Categorie pour le groupement

    // === SIMCONNECT ===
    SimEvent = "TOGGLE_NAV_LIGHTS",  // K:Event pour action simple
    SimEventOn = "...",              // K:Event pour allumer (optionnel)
    SimEventOff = "...",             // K:Event pour eteindre (optionnel)
    SimVar = "LIGHT NAV",            // Variable pour lire l'etat
    SimVarUnit = "Bool",             // Unite: Bool, Number, Knots, Feet, Degrees...

    // === INTERFACE ===
    ControlType = ControlType.Toggle,  // Type de controle
    Hidden = false,                    // true = pas affiche mais etat lu
    IsMomentary = false,               // true = press+release automatique

    // === CLAVIER (mode console) ===
    Key = ConsoleKey.D1,
    KeyDisplay = "1",

    // === SELECTEURS ===
    SelectorOptions = new List<SelectorOption> { ... }  // Si ControlType.Selector
}
```

---

## 3. TYPES DE COMMANDES

### 3.1 Toggle (bouton ON/OFF)

Le plus courant. Un clic = inverse l'etat.

```csharp
new AircraftCommand
{
    Id = "nav_lights",
    SimEvent = "TOGGLE_NAV_LIGHTS",  // Un seul event qui toggle
    SimVar = "LIGHT NAV",            // Pour lire l'etat actuel
    ControlType = ControlType.Toggle
}
```

**Flux:**
1. Client envoie `{type: "command", data: {id: "nav_lights"}}`
2. SimConnect execute `TOGGLE_NAV_LIGHTS`
3. MSFS change l'etat
4. SimVar `LIGHT NAV` change (0→1 ou 1→0)
5. WebSocket broadcast `{type: "state", data: {id: "nav_lights", value: 1}}`
6. Interface met a jour la LED

### 3.2 Toggle avec SimEventOn/Off

Pour les systemes qui necessitent des events separes ON et OFF.

```csharp
new AircraftCommand
{
    Id = "fuel_pump",
    SimEventOn = "FUELSYSTEM_PUMP_ON",    // Event pour allumer
    SimEventOff = "FUELSYSTEM_PUMP_OFF",  // Event pour eteindre
    SimVar = "FUELSYSTEM PUMP SWITCH:1",
    IsMomentary = true  // Important: press+release pour ces events
}
```

**Logique dans SimConnectService:**
```csharp
if (!string.IsNullOrEmpty(command.SimEventOn) && !string.IsNullOrEmpty(command.SimEventOff))
{
    var currentState = GetState(commandId);
    if (currentState > 0.5)
        SendEventByName(command.SimEventOff, value: 0);  // Eteindre
    else
        SendEventByName(command.SimEventOn, value: 1);   // Allumer
}
```

### 3.3 Selector (multi-positions)

Pour les selecteurs comme les volets.

```csharp
new AircraftCommand
{
    Id = "flaps",
    SimVar = "FLAPS HANDLE INDEX:1",
    ControlType = ControlType.Selector,
    SelectorOptions = new List<SelectorOption>
    {
        new SelectorOption { Label = "UP",  SimEvent = "FLAPS_UP", Value = 0 },
        new SelectorOption { Label = "10", SimEvent = "FLAPS_1",  Value = 1 },
        new SelectorOption { Label = "20", SimEvent = "FLAPS_2",  Value = 2 },
        new SelectorOption { Label = "30", SimEvent = "FLAPS_3",  Value = 3 },
    }
}
```

**Flux:**
1. Interface affiche les 4 options
2. Client clique "20" → envoie `{id: "flaps", simEvent: "FLAPS_2", value: 2}`
3. SimConnect execute `FLAPS_2`
4. SimVar `FLAPS HANDLE INDEX:1` passe a 2
5. Interface met a jour l'option selectionnee

### 3.4 Momentary (appui bref)

Pour les boutons qui doivent etre presses puis relaches (ex: incrementer une valeur).

```csharp
new AircraftCommand
{
    Id = "hdg_inc_1",
    SimEvent = "HEADING_BUG_INC",
    ControlType = ControlType.Momentary,
    Hidden = true  // Souvent cache car utilise par l'interface de maniere speciale
}
```

### 3.5 Commandes Hidden

Les commandes avec `Hidden = true` ne sont pas affichees dans l'interface mais:
- Leur SimVar est quand meme lue (pour les afficheurs)
- Elles peuvent etre appelees par d'autres commandes

Exemple: `display_hdg` lit `AUTOPILOT HEADING LOCK DIR` pour afficher la valeur dans l'interface.

---

## 4. LOGIQUE DES INCREMENTS MULTIPLES

Pour les boutons "+10" ou "+1000", la logique repete la commande unitaire plusieurs fois.

**Dans SimConnectService.cs:**

```csharp
// Mapping des commandes multiples vers leur equivalent unitaire
if (commandId == "hdg_inc_10")  { actualCommandId = "hdg_inc_1";  repeatCount = 10; delayMs = 50; }
if (commandId == "hdg_dec_10")  { actualCommandId = "hdg_dec_1";  repeatCount = 10; delayMs = 50; }
if (commandId == "alt_inc_1000") { actualCommandId = "alt_inc_100"; repeatCount = 10; delayMs = 100; }
if (commandId == "alt_dec_1000") { actualCommandId = "alt_dec_100"; repeatCount = 10; delayMs = 100; }

// Execution avec delai entre chaque
for (int i = 0; i < repeatCount; i++)
{
    ExecuteCommand(command, actualCommandId);
    if (repeatCount > 1 && i < repeatCount - 1)
        Thread.Sleep(delayMs);  // Delai necessaire pour que MSFS traite
}
```

**Important:** Les delais sont necessaires car MSFS ne peut pas traiter les events trop rapidement.
- HDG: 50ms entre chaque increment
- ALT: 100ms entre chaque increment (plus lent car plus de calculs)

---

## 5. FLUX DE DONNEES WEBSOCKET

### 5.1 Messages Client → Serveur

```json
// Commande simple (Toggle/Momentary)
{ "type": "command", "data": { "id": "nav_lights" } }

// Commande Selector avec event specifique
{ "type": "command", "data": { "id": "flaps", "simEvent": "FLAPS_2", "value": 2 } }

// Ping keepalive
{ "type": "ping" }
```

### 5.2 Messages Serveur → Client

```json
// Etat de connexion MSFS
{ "type": "connection", "data": { "connected": true } }

// Changement d'avion (envoie le profil complet)
{ "type": "aircraft", "data": { "title": "Cessna...", "profile": {...} } }

// Mise a jour d'etat d'une commande
{ "type": "state", "data": { "id": "nav_lights", "value": 1 } }

// Pong reponse
{ "type": "pong" }
```

---

## 6. AJOUTER UN NOUVEL AVION

### Etape 1: Creer le fichier profil

```csharp
// Profiles/Boeing737Profile.cs
namespace MsfsRemoteButtons.Profiles;

public class Boeing737Profile : IAircraftProfile
{
    public string AircraftName => "Boeing 737-800";
    public string AircraftId => "B738";
    public string Description => "Boeing 737-800";

    public List<AircraftPattern> DetectionPatterns => new()
    {
        new AircraftPattern { Pattern = "737", Contains = true },
        new AircraftPattern { Pattern = "B738", Contains = true },
    };

    public List<string> Categories => new()
    {
        "AUTOPILOT",
        "LUMIERES",
        // ...
    };

    public List<AircraftCommand> Commands => new()
    {
        // Definir les commandes...
    };

    public void ExportSimEventsToFile(string? outputDirectory = null)
    {
        // Copier depuis Cessna172G1000Profile
    }
}
```

### Etape 2: Enregistrer dans ProfileManager

```csharp
// Profiles/ProfileManager.cs
private static readonly List<IAircraftProfile> _profiles = new()
{
    new Cessna172G1000Profile(),
    new Boeing737Profile(),  // Ajouter ici
};
```

### Etape 3: Tester

1. Lancer MSFS avec le 737
2. Lancer l'application
3. Verifier que le profil est auto-detecte via les patterns

---

## 7. AJOUTER UNE NOUVELLE COMMANDE

### Exemple: Ajouter les feux anticollision

1. **Trouver les SimEvents et SimVars** dans la doc MSFS SDK:
   - Event: `TOGGLE_BEACON_LIGHTS`
   - SimVar: `LIGHT BEACON`

2. **Ajouter dans le profil:**

```csharp
new AircraftCommand
{
    Id = "beacon",                        // ID unique
    Name = "Beacon",                      // Nom affiche
    SimEvent = "TOGGLE_BEACON_LIGHTS",    // Event SimConnect
    SimVar = "LIGHT BEACON",              // Variable d'etat
    SimVarUnit = "Bool",
    Category = "LUMIERES",
    Key = ConsoleKey.D2,
    KeyDisplay = "2",
    ControlType = ControlType.Toggle
}
```

3. **Verifier que la categorie existe** dans `Categories` du profil.

---

## 8. CONVENTIONS DE NOMMAGE

### IDs de commandes

| Prefixe | Signification | Exemple |
|---------|---------------|---------|
| `ap_` | Autopilot | `ap_master`, `ap_hdg` |
| `nav_` | Navigation | `nav_lights` |
| `display_` | Afficheur (hidden) | `display_hdg` |
| `hdg_inc/dec_` | Heading +/- | `hdg_inc_10` |
| `alt_inc/dec_` | Altitude +/- | `alt_inc_1000` |
| `vs_inc/dec` | Vertical Speed | `vs_inc` |
| `spd_inc/dec` | Speed | `spd_inc` |
| `flaps_` | Volets | `flaps_incr` |

### Categories

Utiliser des noms courts en MAJUSCULES:
- `AUTOPILOT`
- `LUMIERES`
- `ELECTRIQUE`
- `VOLETS`

---

## 9. POINTS D'ATTENTION

### 9.1 Delais entre commandes

MSFS ne peut pas traiter les events trop vite. Toujours mettre un `Thread.Sleep()` entre les commandes repetees:
- Minimum 50ms pour HDG
- Minimum 100ms pour ALT

### 9.2 SimVar vs SimEvent

- **SimEvent** = Action (ce qu'on envoie a MSFS)
- **SimVar** = Etat (ce qu'on lit de MSFS)

Une commande peut avoir:
- Seulement SimEvent (action sans feedback)
- Seulement SimVar (affichage sans action, Hidden=true)
- Les deux (action + feedback)

### 9.3 IsMomentary

Mettre `IsMomentary = true` quand l'event necessite un press+release:
- Events de type `_ON` / `_OFF`
- Certains boutons physiques simules

### 9.4 Ordre des categories

L'ordre dans `Categories` determine l'ordre d'affichage dans l'interface web.

### 9.5 Detection de profil

Les patterns sont evalues dans l'ordre. Le premier match gagne. Mettre les patterns les plus specifiques en premier:
```csharp
new AircraftPattern { Pattern = "Cessna Skyhawk G1000", Contains = true },  // Specifique
new AircraftPattern { Pattern = "C172", Contains = true },                   // General
```

---

## 10. STRUCTURE DES FICHIERS

```
MsfsRemoteButtons/
├── Program.cs                      # Point d'entree, boucle principale
├── MsfsRemoteButtons.csproj        # Configuration .NET 8.0
├── MsfsRemoteButtons.sln           # Solution Visual Studio
│
├── Services/
│   ├── SimConnectService.cs        # Connexion MSFS, events, SimVars
│   └── WebServerService.cs         # Serveur HTTP/WebSocket
│
├── Profiles/
│   ├── IAircraftProfile.cs         # Interface + classes de base
│   ├── ProfileManager.cs           # Detection auto du profil
│   └── Cessna172G1000Profile.cs    # Profil Cessna 172
│
├── Web/
│   ├── index.html                  # Interface web
│   └── style.css                   # Style cockpit A320
│
└── DLLs SimConnect/
    ├── Microsoft.FlightSimulator.SimConnect.dll
    └── SimConnect.dll
```

---

## 11. DEPENDANCES

- **.NET 8.0** (Windows x64)
- **EmbedIO 3.5.2** (serveur HTTP/WebSocket)
- **MSFS 2024 SDK** (SimConnect)

---

## 12. DEMARRAGE RAPIDE

```bash
# Build
dotnet build

# Run
dotnet run

# Ou via Visual Studio
F5
```

L'interface est accessible sur `http://localhost:8080` ou `http://<IP_LOCALE>:8080`.

---

## 13. RESSOURCES

- [MSFS SDK SimConnect Events](https://docs.flightsimulator.com/html/Programming_Tools/SimConnect/SimConnect_SDK.htm)
- [MSFS SimVars Reference](https://docs.flightsimulator.com/html/Programming_Tools/SimVars/Simulation_Variables.htm)
- [EmbedIO Documentation](https://unosquare.github.io/embedio/)
