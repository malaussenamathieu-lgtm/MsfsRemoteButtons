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
        "LUMIÈRES",     // Contient les 5 lumières extérieures + 4 potentiomètres DIMMING
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
        // B: LIGHTING_NAV_0 prioritaire si Developer Mode actif
        new AircraftCommand
        {
            Id = "nav_lights",
            Name = "Nav",
            SimEvent = "TOGGLE_NAV_LIGHTS",
            InputEvent = "LIGHTING_NAV_0",
            SimVar = "LIGHT NAV",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            ControlType = ControlType.Toggle
        },
        // Feu anticollision rouge (gyrophare sur le ventre/dos)
        new AircraftCommand
        {
            Id = "beacon",
            Name = "Beacon",
            SimEvent = "TOGGLE_BEACON_LIGHTS",
            InputEvent = "LIGHTING_BEACON_1",
            SimVar = "LIGHT BEACON",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            ControlType = ControlType.Toggle
        },

        // Feux stroboscopiques blancs (haute intensité, bouts d'ailes)
        new AircraftCommand
        {
            Id = "strobe",
            Name = "Strobe",
            SimEvent = "STROBES_TOGGLE",
            InputEvent = "LIGHTING_STROBE_0",
            SimVar = "LIGHT STROBE",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            ControlType = ControlType.Toggle
        },

        // Phare d'atterrissage (haute puissance, sur l'aile gauche)
        new AircraftCommand
        {
            Id = "landing_light",
            Name = "Landing",
            SimEvent = "LANDING_LIGHTS_TOGGLE",
            InputEvent = "LIGHTING_LANDING_1",
            SimVar = "LIGHT LANDING",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            ControlType = ControlType.Toggle
        },

        // Phare de roulage (moins puissant, pour le taxiway)
        new AircraftCommand
        {
            Id = "taxi_light",
            Name = "Taxi",
            SimEvent = "TOGGLE_TAXI_LIGHTS",
            InputEvent = "LIGHTING_TAXI_1",
            SimVar = "LIGHT TAXI",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
            ControlType = ControlType.Toggle
        },

        // ============================================
        // ÉLECTRIQUE
        // ============================================
        // Gestion de l'alimentation électrique de l'avion
        // ============================================

        // Batterie principale - Alimente les systèmes quand le moteur est éteint
        // K: event conservé : le B: event ne met pas à jour la SimVar, la LED resterait figée
        new AircraftCommand
        {
            Id = "master_battery",
            Name = "Battery",
            SimEvent = "TOGGLE_MASTER_BATTERY",
            SimVar = "ELECTRICAL MASTER BATTERY",
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            ControlType = ControlType.Toggle
        },

        // Alternateur - Recharge la batterie et alimente les systèmes moteur tournant
        new AircraftCommand
        {
            Id = "master_alternator",
            Name = "Alternator",
            SimEvent = "TOGGLE_MASTER_ALTERNATOR",
            InputEvent = "ELECTRICAL_ALTERNATOR_1",
            SimVar = "GENERAL ENG MASTER ALTERNATOR:1",  // :1 = moteur n°1
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            ControlType = ControlType.Toggle
        },

        // Chauffe-sonde Pitot - évite le givrage
        // K: event PITOT_HEAT_TOGGLE pour écriture, SimVar PITOT HEAT SWITCH:1 pour lecture état
        new AircraftCommand
        {
            Id = "pitot_heat",
            Name = "Pitot Heat",
            SimEvent = "PITOT_HEAT_TOGGLE",
            SimVar = "PITOT HEAT SWITCH:1",
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            ControlType = ControlType.Toggle
        },

        // Pompe à carburant électrique - Pour amorçage et secours
        // B: FUEL_PUMP_1 ou K: SimEventOn/Off (press+release)
        new AircraftCommand
        {
            Id = "fuel_pump",
            Name = "Fuel Pump",
            SimEventOn = "FUELSYSTEM_PUMP_ON",
            SimEventOff = "FUELSYSTEM_PUMP_OFF",
            InputEvent = "FUEL_PUMP_1",
            SimVar = "FUELSYSTEM PUMP SWITCH:1",
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            ControlType = ControlType.Toggle,
            IsMomentary = true
        },

        // Avionics Bus 1 - B: event toggle
        new AircraftCommand
        {
            Id = "avionics_bus_1",
            Name = "Avionics Bus 1",
            InputEvent = "ELECTRICAL_LINE_BUS_1_TO_AVIONICS_BUS_1",
            SimVar = "LINE CONNECTION ON:'BUS_1_To_AVIONICS_BUS_1'_n",
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            ControlType = ControlType.Toggle
        },

        // Avionics Bus 2 - B: event toggle
        new AircraftCommand
        {
            Id = "avionics_bus_2",
            Name = "Avionics Bus 2",
            InputEvent = "ELECTRICAL_LINE_BUS_2_TO_AVIONICS_BUS_2",
            SimVar = "LINE CONNECTION ON:'BUS_2_To_AVIONICS_BUS_2'_n",
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            ControlType = ControlType.Toggle
        },

        // ============================================
        // INTERIOR LIGHTS (Potentiomètres)
        // ============================================
        // 4 potentiomètres pour l'éclairage intérieur du cockpit
        // Layout: 2 lignes x 2 colonnes
        // Ligne 1: SW / CB Panels | STBY IND
        // Ligne 2: PEDESTAL | AVIONICS
        // Pour l'instant: lecture seule (Hidden=false pour affichage)
        // ============================================

        // DIMMING (Potentiomètres éclairage intérieur)
        // ============================================
        // 4 potentiomètres pour l'éclairage intérieur du cockpit
        // Déplacés dans LUMIÈRES mais gardent leur sous-section DIMMING pour l'affichage
        // Layout: 2 lignes x 2 colonnes
        // Ligne 1: SW / CB Panels | STBY IND
        // Ligne 2: PEDESTAL | AVIONICS
        // ============================================

        // SW / CB Panels
        new AircraftCommand
        {
            Id = "interior_panels",
            Name = "SW / CB Panels",
            // Contrôle via B: event (pas de lecture disponible)
            SimVar = null,
            SimVarUnit = null,
            InputEvent = "LIGHTING_PANEL_1",
            InputEventHash = 8210702418028666615UL,
            Category = "LUMIÈRES",  // Déplacé de INTERIOR LIGHTS vers LUMIÈRES
            ControlType = ControlType.Potentiometer,
            Hidden = false
        },

        // STBY IND
        new AircraftCommand
        {
            Id = "interior_stby_ind",
            Name = "STBY IND",
            SimVar = null,
            SimVarUnit = null,
            InputEvent = "LIGHTING_PANEL_2",
            InputEventHash = 13178487316034110786UL,
            Category = "LUMIÈRES",  // Déplacé de INTERIOR LIGHTS vers LUMIÈRES
            ControlType = ControlType.Potentiometer,
            Hidden = false
        },

        // PEDESTAL
        new AircraftCommand
        {
            Id = "interior_pedestal",
            Name = "PEDESTAL",
            SimVar = null,
            SimVarUnit = null,
            InputEvent = "LIGHTING_PEDESTRAL_1",
            InputEventHash = 2385961043412447678UL,
            Category = "LUMIÈRES",  // Déplacé de INTERIOR LIGHTS vers LUMIÈRES
            ControlType = ControlType.Potentiometer,
            Hidden = false
        },

        // AVIONICS
        new AircraftCommand
        {
            Id = "interior_avionics",
            Name = "AVIONICS",
            SimVar = null,
            SimVarUnit = null,
            InputEvent = "LIGHTING_POTENTIOMETER_5",
            InputEventHash = 15349620790358860248UL,
            Category = "LUMIÈRES",  // Déplacé de INTERIOR LIGHTS vers LUMIÈRES
            ControlType = ControlType.Potentiometer,
            Hidden = false
        },

        // CABIN (Potentiomètres éclairage cabine)
        // ============================================
        // 2 potentiomètres pour l'éclairage de la cabine
        // Layout: 1 colonne x 2 lignes
        // ============================================

        // CABIN LEFT
        new AircraftCommand
        {
            Id = "cabin_1",
            Name = "CABIN LEFT",
            SimVar = null,
            SimVarUnit = null,
            InputEvent = "LIGHTING_CABIN_1",
            InputEventHash = 9884642386677037074UL,
            Category = "LUMIÈRES",
            ControlType = ControlType.Potentiometer,
            Hidden = false
        },

        // CABIN RIGHT
        new AircraftCommand
        {
            Id = "cabin_2",
            Name = "CABIN RIGHT",
            SimVar = null,
            SimVarUnit = null,
            InputEvent = "LIGHTING_CABIN_2",
            InputEventHash = 5637503832156611495UL,
            Category = "LUMIÈRES",
            ControlType = ControlType.Potentiometer,
            Hidden = false
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
            ControlType = ControlType.Momentary
        },

        // Frein de parking
        new AircraftCommand
        {
            Id = "parking_brake",
            Name = "Parking Brake",
            SimEvent = "PARKING_BRAKE",
            InputEvent = "LANDING_GEAR_PARKINGBRAKE",
            InputEventHash = 0x7A6EA9FCD6091E2D,
            SimVar = "BRAKE PARKING POSITION",
            SimVarUnit = "Bool",
            Category = "VOLETS",
            SubCategory = "parking brake",
            ControlType = ControlType.Toggle
        },

        // Sélecteur de carburant (3 positions)
        new AircraftCommand
        {
            Id = "fuel_selector",
            Name = "Fuel Selector",
            SimEvent = "FUEL_SELECTOR_SET",
            InputEvent = "FUEL_SELECTOR_1",
            InputEventHash = 0x94DD4F6A97392589,
            // Pas de SimVar - contrôle uniquement (write-only)
            // Les L: vars nécessaires ne sont pas accessibles dans MSFS 2024
            // Tentative avec FUELSYSTEM JUNCTION SETTING:'FuelSelector'_1 : ne fonctionne pas
            Category = "VOLETS",
            SubCategory = "fuel selector",
            ControlType = ControlType.Selector,
            SelectorOptions = new List<SelectorOption>
            {
                new SelectorOption { Label = "LEFT", SimEvent = "FUEL_SELECTOR_SET", Value = 1 }, // Position gauche visuelle = valeur 1 (haut dans le sim)
                new SelectorOption { Label = "TAKEOFF LANDING", SimEvent = "FUEL_SELECTOR_SET", Value = 2 }, // Position haut visuelle = valeur 2 (right dans le sim)
                new SelectorOption { Label = "RIGHT", SimEvent = "FUEL_SELECTOR_SET", Value = 0 }, // Position droite visuelle = valeur 0 (left dans le sim)
            }
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
            InputEvent = "AS1000_AUTOPILOT_AP_PFD",
            SimVar = "AUTOPILOT MASTER",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            ControlType = ControlType.Toggle
        },

        // Flight Director - Affiche les barres de guidage sur le PFD
        // Action: B: event AS1000_AUTOPILOT_FD_PFD. Feedback LED: SimVar global AUTOPILOT FLIGHT DIRECTOR ACTIVE:1
        new AircraftCommand
        {
            Id = "ap_fd",
            Name = "FD",
            SimEvent = "TOGGLE_FLIGHT_DIRECTOR",
            InputEvent = "AS1000_AUTOPILOT_FD_PFD",
            SimVar = "AUTOPILOT FLIGHT DIRECTOR ACTIVE:1",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            ControlType = ControlType.Toggle
        },

        // Heading Hold - Maintient le cap sélectionné (HDG bug)
        new AircraftCommand
        {
            Id = "ap_hdg",
            Name = "HDG",
            SimEvent = "AP_HDG_HOLD",
            InputEvent = "AS1000_AUTOPILOT_HEADING_PFD",
            SimVar = "AUTOPILOT HEADING LOCK",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            ControlType = ControlType.Toggle
        },

        // Altitude Hold - Maintient l'altitude sélectionnée
        new AircraftCommand
        {
            Id = "ap_alt",
            Name = "ALT",
            SimEvent = "AP_ALT_HOLD",
            InputEvent = "AS1000_AUTOPILOT_ALTITUDE_PFD",
            SimVar = "AUTOPILOT ALTITUDE LOCK",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            ControlType = ControlType.Toggle
        },

        // NAV Hold - Suit la route GPS/VOR NAV1
        new AircraftCommand
        {
            Id = "ap_nav",
            Name = "NAV",
            SimEvent = "AP_NAV1_HOLD",
            InputEvent = "AS1000_AUTOPILOT_NAVIGATION_PFD",
            SimVar = "AUTOPILOT NAV1 LOCK",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            ControlType = ControlType.Toggle
        },

        // Vertical Navigation (VNAV) - Mode VNAV du G1000
        new AircraftCommand
        {
            Id = "ap_vnav",
            Name = "VNAV",
            InputEvent = "AS1000_AUTOPILOT_VERTICAL_NAVIGATION_PFD",   // B: event (hash connu)
            InputEventHash = 16400171622324593381UL,                   // 0xE3991D32DC1962E5
            Category = "AUTOPILOT",
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
            ControlType = ControlType.Toggle
        },

        // Back Course - Mode approche back course ILS
        new AircraftCommand
        {
            Id = "ap_bc",
            Name = "BC",
            InputEvent = "AS1000_AUTOPILOT_BACKCOURSE_PFD",
            InputEventHash = 16708404293000148863UL,
            SimVar = "AUTOPILOT APPROACH HOLD",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            ControlType = ControlType.Toggle
        },

        // Vertical Speed Hold - Maintient le taux de montée/descente sélectionné
        new AircraftCommand
        {
            Id = "ap_vs",
            Name = "VS",
            SimEvent = "AP_VS_HOLD",
            InputEvent = "AS1000_AUTOPILOT_VERTICALSPEED_PFD",
            SimVar = "AUTOPILOT VERTICAL HOLD",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            ControlType = ControlType.Toggle
        },

        // Flight Level Change - Maintient la vitesse en montée/descente
        new AircraftCommand
        {
            Id = "ap_flc",
            Name = "FLC",
            InputEvent = "AS1000_AUTOPILOT_FLIGHTLEVELCHANGE_PFD",
            InputEventHash = 14970840394202116082UL, // 0xCFC31C05057C6FF2
            SimEvent = "FLIGHT_LEVEL_CHANGE",
            SimVar = "AUTOPILOT FLIGHT LEVEL CHANGE",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
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
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "spd_dec",
            Name = "SPD-",
            SimEvent = "AP_SPD_VAR_DEC",
            Category = "AUTOPILOT",
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
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "hdg_dec_1",
            Name = "HDG-1",
            SimEvent = "HEADING_BUG_DEC",
            Category = "AUTOPILOT",
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
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "hdg_dec_10",
            Name = "HDG-10",
            SimEvent = "HEADING_BUG_DEC",
            Category = "AUTOPILOT",
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
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "alt_dec_100",
            Name = "ALT-100",
            SimEvent = "AP_ALT_VAR_DEC",
            Category = "AUTOPILOT",
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
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "alt_dec_1000",
            Name = "ALT-1000",
            SimEvent = "AP_ALT_VAR_DEC",
            Category = "AUTOPILOT",
            ControlType = ControlType.Momentary,
            Hidden = true
        },

        // --- VITESSE VERTICALE (VS) ---
        // Incrément: +100 ft/min, Décrément: -100 ft/min
        new AircraftCommand
        {
            Id = "vs_inc",
            Name = "VS UP",
            InputEvent = "AS1000_AUTOPILOT_VERTICALSPEED_UP_PFD",
            InputEventHash = 11862786159055984339UL, // 0xA4A11528F0F3D6D3
            Category = "AUTOPILOT",
            ControlType = ControlType.Momentary,
            Hidden = true
        },
        new AircraftCommand
        {
            Id = "vs_dec",
            Name = "VS DN",
            InputEvent = "AS1000_AUTOPILOT_VERTICALSPEED_DOWN_PFD",
            InputEventHash = 1969240319255845983UL, // 0x1B5425A30A9F1C5F
            Category = "AUTOPILOT",
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
            ControlType = ControlType.Toggle,
            Hidden = true  // Affichage uniquement, pas de bouton
        },

        // Trim de profondeur (affichage uniquement)
        new AircraftCommand
        {
            Id = "display_elevator_trim",
            Name = "Elevator Trim",
            SimVar = "ELEVATOR TRIM PCT",
            SimVarUnit = "Percent Over 100",
            Category = "VOLETS",
            SubCategory = "trim",
            ControlType = ControlType.Toggle,
            Hidden = true  // Affichage uniquement, pas de bouton
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
