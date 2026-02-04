namespace MsfsRemoteButtons.Profiles;

// ============================================================================
// PROFIL CESSNA 172 SKYHAWK G1000
// ============================================================================
//
// Ce fichier définit toutes les commandes disponibles pour le Cessna 172
// avec cockpit Garmin G1000 dans MSFS 2024.
//
// STRUCTURE D'UNE COMMANDE:
// - Id: Identifiant unique utilisé partout (WebSocket, états, etc.)
// - Name: Nom affiché dans l'interface web
// - SimEvent: K:Event SimConnect pour déclencher l'action
// - SimEventOn/Off: Events séparés pour ON et OFF (si nécessaire)
// - SimVar: Variable SimConnect pour lire l'état actuel
// - SimVarUnit: Unité de la SimVar (Bool, Number, Knots, Feet, Degrees, etc.)
// - Category: Groupe d'affichage dans l'interface
// - ControlType: Toggle (ON/OFF), Selector (multi-positions), Momentary (appui bref)
// - Hidden: true = lit l'état mais n'affiche pas de bouton (pour les afficheurs)
// - IsMomentary: true = simule press+release (nécessaire pour certains events)
//
// COMMENT TROUVER LES EVENTS ET SIMVARS:
// - Documentation MSFS SDK: https://docs.flightsimulator.com
// - Outil MSFS DevMode > SimConnect Inspector
// - Fichier exporté: Desktop/SimEvents/C172_SimEvents.txt
//
// ============================================================================

/// <summary>
/// Profil pour le Cessna 172 Skyhawk G1000
/// </summary>
public class Cessna172G1000Profile : IAircraftProfile
{
    public string AircraftName => "Cessna 172 Skyhawk G1000";
    public string AircraftId => "C172";
    public string Description => "Cessna 172 avec cockpit Garmin G1000";

    // Patterns pour détecter automatiquement cet avion via le titre MSFS
    // Le premier pattern qui matche est utilisé (ordre important)
    public List<AircraftPattern> DetectionPatterns => new()
    {
        new AircraftPattern { Pattern = "Cessna Skyhawk G1000", Contains = true },  // Pattern spécifique en premier
        new AircraftPattern { Pattern = "C172", Contains = true },
        new AircraftPattern { Pattern = "Cessna 172", Contains = true },            // Pattern générique en dernier
    };

    // Ordre d'affichage des catégories dans l'interface web
    public List<string> Categories => new()
    {
        "AUTOPILOT",    // En haut car le plus utilisé en vol
        "LUMIÈRES",
        "VOLETS",
        "ÉLECTRIQUE",
    };

    // ========================================================================
    // LISTE DES COMMANDES
    // ========================================================================
    // Organisation:
    // 1. LUMIÈRES - Feux de navigation, anticollision, atterrissage
    // 2. ÉLECTRIQUE - Batterie, alternateur, pompe à carburant
    // 3. VOLETS - Sélecteur de position des volets
    // 4. AUTOPILOT - Modes AP, boutons de contrôle, afficheurs (hidden)
    // ========================================================================

    public List<AircraftCommand> Commands => new()
    {
        // ============================================
        // LUMIÈRES
        // ============================================
        // Tous les feux utilisent des events TOGGLE_* qui inversent l'état
        // SimVar retourne 0 (éteint) ou 1 (allumé)
        // ============================================

        // Feux de navigation (bouts d'ailes: rouge gauche, vert droite, blanc arrière)
        new AircraftCommand
        {
            Id = "nav_lights",
            Name = "Nav",
            SimEvent = "TOGGLE_NAV_LIGHTS",
            SimVar = "LIGHT NAV",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            Key = ConsoleKey.D1,
            KeyDisplay = "1",
            ControlType = ControlType.Toggle
        },
        // Feu anticollision rouge (gyrophare sur le ventre/dos)
        new AircraftCommand
        {
            Id = "beacon",
            Name = "Beacon",
            SimEvent = "TOGGLE_BEACON_LIGHTS",
            SimVar = "LIGHT BEACON",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            Key = ConsoleKey.D2,
            KeyDisplay = "2",
            ControlType = ControlType.Toggle
        },

        // Feux stroboscopiques blancs (haute intensité, bouts d'ailes)
        new AircraftCommand
        {
            Id = "strobe",
            Name = "Strobe",
            SimEvent = "STROBES_TOGGLE",
            SimVar = "LIGHT STROBE",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            Key = ConsoleKey.D3,
            KeyDisplay = "3",
            ControlType = ControlType.Toggle
        },

        // Phare d'atterrissage (haute puissance, sur l'aile gauche)
        new AircraftCommand
        {
            Id = "landing_light",
            Name = "Landing",
            SimEvent = "LANDING_LIGHTS_TOGGLE",
            SimVar = "LIGHT LANDING",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            Key = ConsoleKey.D4,
            KeyDisplay = "4",
            ControlType = ControlType.Toggle
        },

        // Phare de roulage (moins puissant, pour le taxiway)
        new AircraftCommand
        {
            Id = "taxi_light",
            Name = "Taxi",
            SimEvent = "TOGGLE_TAXI_LIGHTS",
            SimVar = "LIGHT TAXI",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            Key = ConsoleKey.D5,
            KeyDisplay = "5",
            ControlType = ControlType.Toggle
        },

        // ============================================
        // ÉLECTRIQUE
        // ============================================
        // Gestion de l'alimentation électrique de l'avion
        // ============================================

        // Batterie principale - Alimente les systèmes quand le moteur est éteint
        new AircraftCommand
        {
            Id = "master_battery",
            Name = "Battery",
            SimEvent = "TOGGLE_MASTER_BATTERY",
            SimVar = "ELECTRICAL MASTER BATTERY",
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            Key = ConsoleKey.B,
            KeyDisplay = "B",
            ControlType = ControlType.Toggle
        },

        // Alternateur - Recharge la batterie et alimente les systèmes moteur tournant
        new AircraftCommand
        {
            Id = "master_alternator",
            Name = "Alternator",
            SimEvent = "TOGGLE_MASTER_ALTERNATOR",
            SimVar = "GENERAL ENG MASTER ALTERNATOR:1",  // :1 = moteur n°1
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            Key = ConsoleKey.A,
            KeyDisplay = "A",
            ControlType = ControlType.Toggle
        },

        // Pompe à carburant électrique - Pour amorçage et secours
        // ATTENTION: Utilise SimEventOn/Off car FUELSYSTEM_PUMP_TOGGLE n'existe pas
        // IsMomentary = true car ces events nécessitent un press+release
        new AircraftCommand
        {
            Id = "fuel_pump",
            Name = "Fuel Pump",
            SimEventOn = "FUELSYSTEM_PUMP_ON",
            SimEventOff = "FUELSYSTEM_PUMP_OFF",
            SimVar = "FUELSYSTEM PUMP SWITCH:1",
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            Key = ConsoleKey.P,
            KeyDisplay = "P",
            ControlType = ControlType.Toggle,
            IsMomentary = true
        },

        // ============================================
        // VOLETS (Sélecteur + boutons)
        // ============================================
        // Le Cessna 172 a 4 positions de volets: UP, 10°, 20°, FULL (30°)
        // L'interface affiche: bouton [-] + sélecteur + bouton [+]
        // ============================================

        // Bouton pour rétracter les volets d'un cran
        new AircraftCommand
        {
            Id = "flaps_decr",
            Name = "−",
            SimEvent = "FLAPS_DECR",
            Category = "VOLETS",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary
        },

        // Sélecteur de position des volets
        // SimVar retourne l'index (0-3), les options définissent l'event pour chaque position
        new AircraftCommand
        {
            Id = "flaps",
            Name = "Flaps",
            SimVar = "FLAPS HANDLE INDEX:1",
            SimVarUnit = "Number",
            Category = "VOLETS",
            Key = ConsoleKey.F,
            KeyDisplay = "F",
            ControlType = ControlType.Selector,
            SelectorOptions = new List<SelectorOption>
            {
                new SelectorOption { Label = "UP", SimEvent = "FLAPS_UP", Value = 0 },    // Volets rentrés
                new SelectorOption { Label = "10°", SimEvent = "FLAPS_1", Value = 1 },   // Premier cran
                new SelectorOption { Label = "20°", SimEvent = "FLAPS_2", Value = 2 },   // Approche
                new SelectorOption { Label = "30°", SimEvent = "FLAPS_3", Value = 3 },   // Atterrissage (FULL)
            }
        },

        // Bouton pour sortir les volets d'un cran
        new AircraftCommand
        {
            Id = "flaps_incr",
            Name = "+",
            SimEvent = "FLAPS_INCR",
            Category = "VOLETS",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary
        },

        // ============================================
        // AUTOPILOT
        // ============================================
        // Le G1000 a un autopilot complet avec plusieurs modes
        // Les boutons principaux sont affichés, les contrôles de valeurs sont hidden
        // ============================================

        // AP Master - Active/désactive l'autopilot
        new AircraftCommand
        {
            Id = "ap_master",
            Name = "AP",
            SimEvent = "AP_MASTER",
            SimVar = "AUTOPILOT MASTER",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            Key = ConsoleKey.Z,
            KeyDisplay = "Z",
            ControlType = ControlType.Toggle
        },

        // Flight Director - Affiche les barres de guidage sur le PFD
        new AircraftCommand
        {
            Id = "ap_fd",
            Name = "FD",
            SimEvent = "TOGGLE_FLIGHT_DIRECTOR",
            SimVar = "AUTOPILOT FLIGHT DIRECTOR ACTIVE",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            Key = ConsoleKey.D,
            KeyDisplay = "D",
            ControlType = ControlType.Toggle
        },

        // Flight Level Change - Maintient la vitesse en montée/descente
        new AircraftCommand
        {
            Id = "ap_flc",
            Name = "FLC",
            SimEvent = "FLIGHT_LEVEL_CHANGE",
            SimVar = "AUTOPILOT FLIGHT LEVEL CHANGE",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Toggle
        },

        // Heading Hold - Maintient le cap sélectionné (HDG bug)
        new AircraftCommand
        {
            Id = "ap_hdg",
            Name = "HDG",
            SimEvent = "AP_HDG_HOLD",
            SimVar = "AUTOPILOT HEADING LOCK",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            Key = ConsoleKey.H,
            KeyDisplay = "H",
            ControlType = ControlType.Toggle
        },

        // Altitude Hold - Maintient l'altitude sélectionnée
        new AircraftCommand
        {
            Id = "ap_alt",
            Name = "ALT",
            SimEvent = "AP_ALT_HOLD",
            SimVar = "AUTOPILOT ALTITUDE LOCK",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            Key = ConsoleKey.T,
            KeyDisplay = "T",
            ControlType = ControlType.Toggle
        },

        // NAV Hold - Suit la route GPS/VOR NAV1
        new AircraftCommand
        {
            Id = "ap_nav",
            Name = "NAV",
            SimEvent = "AP_NAV1_HOLD",
            SimVar = "AUTOPILOT NAV1 LOCK",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            Key = ConsoleKey.N,
            KeyDisplay = "N",
            ControlType = ControlType.Toggle
        },

        // Approach Hold - Mode approche ILS/RNAV
        new AircraftCommand
        {
            Id = "ap_apr",
            Name = "APR",
            SimEvent = "AP_APR_HOLD",
            SimVar = "AUTOPILOT APPROACH HOLD",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            Key = ConsoleKey.R,
            KeyDisplay = "R",
            ControlType = ControlType.Toggle
        },

        // Vertical Speed Hold - Maintient le taux de montée/descente sélectionné
        new AircraftCommand
        {
            Id = "ap_vs",
            Name = "VS",
            SimEvent = "AP_VS_HOLD",
            SimVar = "AUTOPILOT VERTICAL HOLD",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            Key = ConsoleKey.W,
            KeyDisplay = "W",
            ControlType = ControlType.Toggle
        },

        // ============================================
        // CONTRÔLES AUTOPILOT (Hidden)
        // ============================================
        // Ces commandes ne sont pas affichées comme boutons mais sont utilisées
        // par l'interface web pour les boutons +/- des valeurs AP
        // ============================================

        // --- VITESSE (SPD) ---
        // Incrément/décrément de la vitesse cible (pour FLC)
        new AircraftCommand
        {
            Id = "spd_inc",
            Name = "SPD+",
            SimEvent = "AP_SPD_VAR_INC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "spd_dec",
            Name = "SPD-",
            SimEvent = "AP_SPD_VAR_DEC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },

        // --- CAP (HDG) ---
        // hdg_inc/dec_1: Incrément unitaire (1°)
        // hdg_inc/dec_10: Géré dans SimConnectService (répète 10x hdg_inc/dec_1)
        new AircraftCommand
        {
            Id = "hdg_inc_1",
            Name = "HDG+1",
            SimEvent = "HEADING_BUG_INC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "hdg_dec_1",
            Name = "HDG-1",
            SimEvent = "HEADING_BUG_DEC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        // NOTE: hdg_inc_10 et hdg_dec_10 utilisent le même SimEvent
        // La répétition x10 est gérée dans SimConnectService.SendCommand()
        new AircraftCommand
        {
            Id = "hdg_inc_10",
            Name = "HDG+10",
            SimEvent = "HEADING_BUG_INC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "hdg_dec_10",
            Name = "HDG-10",
            SimEvent = "HEADING_BUG_DEC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },

        // --- ALTITUDE (ALT) ---
        // alt_inc/dec_100: Incrément unitaire (100ft)
        // alt_inc/dec_1000: Géré dans SimConnectService (répète 10x alt_inc/dec_100)
        new AircraftCommand
        {
            Id = "alt_inc_100",
            Name = "ALT+100",
            SimEvent = "AP_ALT_VAR_INC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "alt_dec_100",
            Name = "ALT-100",
            SimEvent = "AP_ALT_VAR_DEC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        // NOTE: alt_inc_1000 utilise le même SimEvent, répété 10x
        new AircraftCommand
        {
            Id = "alt_inc_1000",
            Name = "ALT+1000",
            SimEvent = "AP_ALT_VAR_INC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "alt_dec_1000",
            Name = "ALT-1000",
            SimEvent = "AP_ALT_VAR_DEC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },

        // --- VITESSE VERTICALE (VS) ---
        // Incrément: +100 ft/min, Décrément: -100 ft/min
        new AircraftCommand
        {
            Id = "vs_inc",
            Name = "VS UP",
            SimEvent = "AP_VS_VAR_INC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "vs_dec",
            Name = "VS DN",
            SimEvent = "AP_VS_VAR_DEC",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Momentary,
            Hidden = true
        },

        // ============================================
        // AFFICHEURS AUTOPILOT (Hidden)
        // ============================================
        // Ces commandes n'ont pas de SimEvent, elles servent uniquement
        // à lire les valeurs affichées sur le panneau AP (SPD, HDG, ALT, VS)
        // L'interface web utilise ces valeurs pour les afficheurs numériques
        // ============================================

        // Afficheur vitesse (en noeuds)
        new AircraftCommand
        {
            Id = "display_spd",
            Name = "SPD Display",
            SimVar = "AUTOPILOT AIRSPEED HOLD VAR",
            SimVarUnit = "Knots",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Toggle,  // ControlType ignoré pour Hidden
            Hidden = true
        },

        // Afficheur cap (en degrés, 0-359)
        new AircraftCommand
        {
            Id = "display_hdg",
            Name = "HDG Display",
            SimVar = "AUTOPILOT HEADING LOCK DIR",
            SimVarUnit = "Degrees",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Toggle,
            Hidden = true
        },

        // Afficheur altitude (en pieds)
        new AircraftCommand
        {
            Id = "display_alt",
            Name = "ALT Display",
            SimVar = "AUTOPILOT ALTITUDE LOCK VAR",
            SimVarUnit = "Feet",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Toggle,
            Hidden = true
        },

        // Afficheur vitesse verticale (en ft/min, positif = montée, négatif = descente)
        new AircraftCommand
        {
            Id = "display_vs",
            Name = "VS Display",
            SimVar = "AUTOPILOT VERTICAL HOLD VAR",
            SimVarUnit = "Feet per minute",
            Category = "AUTOPILOT",
            Key = ConsoleKey.NoName,
            KeyDisplay = "",
            ControlType = ControlType.Toggle,
            Hidden = true
        },
    };

    // ========================================================================
    // MÉTHODES UTILITAIRES
    // ========================================================================

    /// <summary>
    /// Exporte tous les SimEvents du profil dans un fichier texte
    ///
    /// Utile pour:
    /// - Débugger les events qui ne fonctionnent pas
    /// - Documenter les events utilisés
    /// - Référence rapide sans ouvrir le code
    ///
    /// Fichier créé: Desktop/SimEvents/{AircraftId}_SimEvents.txt
    /// </summary>
    public void ExportSimEventsToFile(string? outputDirectory = null)
    {
        var simEvents = new HashSet<string>();

        foreach (var command in Commands)
        {
            if (!string.IsNullOrEmpty(command.SimEvent))
                simEvents.Add(command.SimEvent);

            if (!string.IsNullOrEmpty(command.SimEventOn))
                simEvents.Add(command.SimEventOn);

            if (!string.IsNullOrEmpty(command.SimEventOff))
                simEvents.Add(command.SimEventOff);

            if (command.SelectorOptions != null)
            {
                foreach (var option in command.SelectorOptions)
                {
                    if (!string.IsNullOrEmpty(option.SimEvent))
                        simEvents.Add(option.SimEvent);
                }
            }
        }

        try
        {
            // Essayer le Bureau en priorité, sinon le dossier courant
            var directory = outputDirectory;
            if (string.IsNullOrEmpty(directory))
            {
                directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SimEvents");
                if (!Directory.Exists(directory))
                {
                    directory = AppContext.BaseDirectory;
                }
            }

            Directory.CreateDirectory(directory);

            var fileName = $"{AircraftId}_SimEvents.txt";
            var filePath = Path.Combine(directory, fileName);
            var content = string.Join(Environment.NewLine, simEvents.OrderBy(e => e));

            File.WriteAllText(filePath, content);
            System.Diagnostics.Debug.WriteLine($"✅ Fichier créé: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Erreur export: {ex.Message}");
        }
    }
}
