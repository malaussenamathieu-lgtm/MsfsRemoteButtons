# MSFS 2024 Remote Buttons - Test Console

Application console pour tester les commandes SimConnect avant de passer au réseau.

## 📋 Prérequis

1. **MSFS 2024** installé et lancé
2. **MSFS 2024 SDK** installé (via le menu Dev Mode dans MSFS)
3. **Visual Studio Community 2022** avec workload ".NET Desktop Development"

## 🔧 Installation

### Étape 1 : Installer le SDK MSFS 2024

1. Lance MSFS 2024
2. Active le **Developer Mode** (Options → General → Developers)
3. En haut de l'écran, menu **Help → SDK Installer**
4. Installe le SDK (par défaut dans `C:\MSFS 2024 SDK\`)

### Étape 2 : Copier la DLL SimConnect

Copie ce fichier :
```
C:\MSFS 2024 SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll
```

Dans le dossier de ce projet (à côté de `MsfsRemoteButtons.csproj`)

### Étape 3 : Ouvrir dans Visual Studio

1. Double-clique sur `MsfsRemoteButtons.csproj` ou ouvre-le avec VS
2. Visual Studio va charger le projet

### Étape 4 : Compiler et exécuter

1. En haut, sélectionne **Release** et **x64**
2. `Ctrl+Shift+B` pour compiler
3. `F5` pour lancer (ou `Ctrl+F5` sans debug)

## 🎮 Utilisation

1. **Lance MSFS 2024** et charge un vol (tu dois être dans l'avion)
2. **Lance l'application** console
3. Appuie sur `C` pour connecter
4. Utilise les touches du clavier pour activer les commandes

## 🔑 Commandes disponibles

| Touche | Action |
|--------|--------|
| **LUMIÈRES** ||
| 1 | Nav Lights |
| 2 | Beacon |
| 3 | Landing Lights |
| 4 | Taxi Lights |
| 5 | Strobes |
| 6 | Panel Lights |
| **ÉLECTRIQUE** ||
| B | Battery Master |
| A | Alternator |
| V | Avionics Master |
| **VOL** ||
| G | Gear Toggle |
| F | Flaps Down |
| R | Flaps Up |
| P | Parking Brake |
| **AUTOPILOT** ||
| Z | AP Master |
| H | Heading Hold |
| L | Altitude Hold |
| N | NAV Hold |

## ❗ Problèmes courants

### "Erreur de connexion"
- MSFS 2024 doit être lancé **avant** l'application
- Tu dois être **dans un vol** (pas juste le menu principal)
- Vérifie que le SDK est bien installé

### "DLL introuvable"
- Copie `Microsoft.FlightSimulator.SimConnect.dll` dans le dossier du projet
- Recompile le projet

### "L'application plante au démarrage"
- Vérifie que tu compiles en **x64** (pas x86 ou Any CPU)

## 📁 Structure du projet

```
msfs-remote-buttons/
├── MsfsRemoteButtons.csproj    # Fichier projet
├── Program.cs                   # Code source
├── Microsoft.FlightSimulator.SimConnect.dll  # À copier ici !
└── README.md                    # Ce fichier
```

## 🚀 Prochaine étape

Une fois que ça marche en local, on passera à la version réseau avec :
- Un serveur qui tourne sur le PC MSFS
- Un client (web ou app) sur l'autre PC/tablette
