namespace MsfsRemoteButtons.Profiles;

// ============================================================================
// PROFIL PILATUS PC-12 NGX
// ============================================================================
//
// Profil pour le Pilatus PC-12 NGX (MSFS 2024).
// Commandes basées sur les K: events SimConnect (compatibles tous avions).
// Les B: InputEvents pourront être ajoutés après export depuis le DevMode MSFS.
//
// ============================================================================

/// <summary>
/// Profil pour le Pilatus PC-12 NGX
/// </summary>
public class PilatusPC12Profile : IAircraftProfile
{
    public string AircraftName => "Pilatus PC-12 NGX";
    public string AircraftId => "PC12";
    public string Description => "Pilatus PC-12 NGX - Turboprop";

    // Patterns pour détection automatique (ordre: du plus spécifique au plus générique)
    public List<AircraftPattern> DetectionPatterns => new()
    {
        new AircraftPattern { Pattern = "Pilatus PC-12 NGX", Contains = true },
        new AircraftPattern { Pattern = "PC-12 NGX", Contains = true },
        new AircraftPattern { Pattern = "PC-12", Contains = true },
        new AircraftPattern { Pattern = "PC12", Contains = true },
        new AircraftPattern { Pattern = "Pilatus PC-12", Contains = true },
    };

    public List<string> Categories => new()
    {
        "AUTOPILOT",
        "LUMIÈRES",
        "VOLETS",
        "ÉLECTRIQUE",
    };

    public List<AircraftCommand> Commands => new()
    {
        // ============================================
        // LUMIÈRES (K: events standard)
        // ============================================
        new AircraftCommand
        {
            Id = "nav_lights",
            Name = "Nav",
            SimEvent = "TOGGLE_NAV_LIGHTS",
            SimVar = "LIGHT NAV",
            SimVarUnit = "Bool",
            Category = "LUMIÈRES",
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
            ControlType = ControlType.Toggle
        },
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
        new AircraftCommand
        {
            Id = "fuel_pump",
            Name = "Fuel Pump",
            SimEventOn = "FUELSYSTEM_PUMP_ON",
            SimEventOff = "FUELSYSTEM_PUMP_OFF",
            SimVar = "FUELSYSTEM PUMP SWITCH:1",
            SimVarUnit = "Bool",
            Category = "ÉLECTRIQUE",
            ControlType = ControlType.Toggle,
            IsMomentary = true
        },

        // ============================================
        // VOLETS
        // ============================================
        new AircraftCommand
        {
            Id = "flaps_decr",
            Name = "−",
            SimEvent = "FLAPS_DECR",
            Category = "VOLETS",
            ControlType = ControlType.Momentary
        },
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
                new SelectorOption { Label = "UP", SimEvent = "FLAPS_UP", Value = 0 },
                new SelectorOption { Label = "1", SimEvent = "FLAPS_1", Value = 1 },
                new SelectorOption { Label = "2", SimEvent = "FLAPS_2", Value = 2 },
                new SelectorOption { Label = "3", SimEvent = "FLAPS_3", Value = 3 },
                new SelectorOption { Label = "FULL", SimEvent = "FLAPS_4", Value = 4 },
            }
        },
        new AircraftCommand
        {
            Id = "flaps_incr",
            Name = "+",
            SimEvent = "FLAPS_INCR",
            Category = "VOLETS",
            ControlType = ControlType.Momentary
        },
        new AircraftCommand
        {
            Id = "parking_brake",
            Name = "Parking Brake",
            SimEvent = "PARKING_BRAKE",
            SimVar = "BRAKE PARKING POSITION",
            SimVarUnit = "Bool",
            Category = "VOLETS",
            ControlType = ControlType.Toggle
        },

        // ============================================
        // AUTOPILOT (K: events standard)
        // ============================================
        new AircraftCommand
        {
            Id = "ap_master",
            Name = "AP",
            SimEvent = "AP_MASTER",
            SimVar = "AUTOPILOT MASTER",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            ControlType = ControlType.Toggle
        },
        new AircraftCommand
        {
            Id = "ap_fd",
            Name = "FD",
            SimEvent = "TOGGLE_FLIGHT_DIRECTOR",
            SimVar = "AUTOPILOT FLIGHT DIRECTOR ACTIVE:1",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
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
            ControlType = ControlType.Toggle
        },
        new AircraftCommand
        {
            Id = "ap_flc",
            Name = "FLC",
            SimEvent = "FLIGHT_LEVEL_CHANGE",
            SimVar = "AUTOPILOT FLIGHT LEVEL CHANGE",
            SimVarUnit = "Bool",
            Category = "AUTOPILOT",
            ControlType = ControlType.Toggle
        },

        // Contrôles AP (hidden)
        new AircraftCommand { Id = "spd_inc", Name = "SPD+", SimEvent = "AP_SPD_VAR_INC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "spd_dec", Name = "SPD-", SimEvent = "AP_SPD_VAR_DEC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "hdg_inc_1", Name = "HDG+1", SimEvent = "HEADING_BUG_INC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "hdg_dec_1", Name = "HDG-1", SimEvent = "HEADING_BUG_DEC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "hdg_inc_10", Name = "HDG+10", SimEvent = "HEADING_BUG_INC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "hdg_dec_10", Name = "HDG-10", SimEvent = "HEADING_BUG_DEC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "alt_inc_100", Name = "ALT+100", SimEvent = "AP_ALT_VAR_INC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "alt_dec_100", Name = "ALT-100", SimEvent = "AP_ALT_VAR_DEC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "alt_inc_1000", Name = "ALT+1000", SimEvent = "AP_ALT_VAR_INC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "alt_dec_1000", Name = "ALT-1000", SimEvent = "AP_ALT_VAR_DEC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "vs_inc", Name = "VS UP", SimEvent = "AP_VS_VAR_INC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },
        new AircraftCommand { Id = "vs_dec", Name = "VS DN", SimEvent = "AP_VS_VAR_DEC", Category = "AUTOPILOT", ControlType = ControlType.Momentary, Hidden = true },

        // Afficheurs (hidden)
        new AircraftCommand { Id = "display_spd", Name = "SPD Display", SimVar = "AUTOPILOT AIRSPEED HOLD VAR", SimVarUnit = "Knots", Category = "AUTOPILOT", ControlType = ControlType.Toggle, Hidden = true },
        new AircraftCommand { Id = "display_hdg", Name = "HDG Display", SimVar = "AUTOPILOT HEADING LOCK DIR", SimVarUnit = "Degrees", Category = "AUTOPILOT", ControlType = ControlType.Toggle, Hidden = true },
        new AircraftCommand { Id = "display_alt", Name = "ALT Display", SimVar = "AUTOPILOT ALTITUDE LOCK VAR", SimVarUnit = "Feet", Category = "AUTOPILOT", ControlType = ControlType.Toggle, Hidden = true },
        new AircraftCommand { Id = "display_vs", Name = "VS Display", SimVar = "AUTOPILOT VERTICAL HOLD VAR", SimVarUnit = "Feet per minute", Category = "AUTOPILOT", ControlType = ControlType.Toggle, Hidden = true },
    };

    public void ExportSimEventsToFile(string? outputDirectory = null)
    {
        var simEvents = new HashSet<string>();

        foreach (var command in Commands)
        {
            if (!string.IsNullOrEmpty(command.SimEvent)) simEvents.Add(command.SimEvent);
            if (!string.IsNullOrEmpty(command.SimEventOn)) simEvents.Add(command.SimEventOn);
            if (!string.IsNullOrEmpty(command.SimEventOff)) simEvents.Add(command.SimEventOff);
            if (command.SelectorOptions != null)
            {
                foreach (var option in command.SelectorOptions)
                {
                    if (!string.IsNullOrEmpty(option.SimEvent)) simEvents.Add(option.SimEvent);
                }
            }
        }

        try
        {
            var directory = outputDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SimEvents");
            if (!Directory.Exists(directory)) directory = AppContext.BaseDirectory;
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, $"{AircraftId}_SimEvents.txt");
            File.WriteAllText(filePath, string.Join(Environment.NewLine, simEvents.OrderBy(e => e)));
            System.Diagnostics.Debug.WriteLine($"✅ Fichier créé: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Erreur export: {ex.Message}");
        }
    }
}
