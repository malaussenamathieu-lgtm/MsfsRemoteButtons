namespace MsfsRemoteButtons.Profiles;

/// <summary>
/// Profil pour le Cessna 172 Skyhawk G1000
/// </summary>
public class Cessna172G1000Profile : IAircraftProfile
{
    public string AircraftName => "Cessna 172 Skyhawk G1000";
    public string AircraftId => "C172";
    public string Description => "Cessna 172 avec cockpit Garmin G1000";

    // Patterns pour détecter automatiquement cet avion
    public List<AircraftPattern> DetectionPatterns => new()
    {
        new AircraftPattern { Pattern = "Cessna Skyhawk G1000", Contains = true },
        new AircraftPattern { Pattern = "C172", Contains = true },
        new AircraftPattern { Pattern = "Cessna 172", Contains = true },
    };

    public List<string> Categories => new()
    {
        "LUMIÈRES",
        "ÉLECTRIQUE",
        "MOTEUR",
        "VOLETS",
        "AUTOPILOT",
    };

    public List<AircraftCommand> Commands => new()
    {
        // ============================================
        // LUMIÈRES
        // ============================================
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
        new AircraftCommand
        {
            Id = "master_alternator",
            Name = "Alternator",
            SimEvent = "TOGGLE_MASTER_ALTERNATOR",
            SimVar = "GENERAL ENG MASTER ALTERNATOR:1",
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            Key = ConsoleKey.A,
            KeyDisplay = "A",
            ControlType = ControlType.Toggle
        },

        // ============================================
        // MOTEUR
        // ============================================
        new AircraftCommand
        {
            Id = "fuel_pump",
            Name = "Fuel Pump",
            SimEventOn = "FUELSYSTEM_PUMP_ON",
            SimEventOff = "FUELSYSTEM_PUMP_OFF",
            SimVar = "FUELSYSTEM PUMP SWITCH:1",
            SimVarUnit = "Bool",
            Category = "MOTEUR",
            Key = ConsoleKey.P,
            KeyDisplay = "P",
            ControlType = ControlType.Toggle,
            IsMomentary = true
        },

        // ============================================
        // VOLETS (Sélecteur + boutons)
        // ============================================
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
                new SelectorOption { Label = "UP", SimEvent = "FLAPS_UP", Value = 0 },
                new SelectorOption { Label = "10°", SimEvent = "FLAPS_1", Value = 1 },
                new SelectorOption { Label = "20°", SimEvent = "FLAPS_2", Value = 2 },
                new SelectorOption { Label = "30°", SimEvent = "FLAPS_3", Value = 3 },
            }
        },
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
    };

    /// <summary>
    /// Exporte les SimEvents du profil dans un fichier texte
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
