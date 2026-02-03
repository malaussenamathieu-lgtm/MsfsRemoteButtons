namespace MsfsRemoteButtons.Profiles;

/// <summary>
/// Gestionnaire des profils d'avion disponibles
/// </summary>
public static class ProfileManager
{
    private static readonly List<IAircraftProfile> _profiles = new()
    {
        new Cessna172G1000Profile(),
        // Ajouter d'autres profils ici plus tard:
        // new Boeing737Profile(),
        // new A320NeoProfile(),
    };

    /// <summary>
    /// Profil par défaut quand aucun avion n'est détecté
    /// </summary>
    public static IAircraftProfile DefaultProfile => _profiles[0];

    /// <summary>
    /// Retourne tous les profils disponibles
    /// </summary>
    public static IReadOnlyList<IAircraftProfile> AvailableProfiles => _profiles;

    /// <summary>
    /// Trouve un profil par son ID
    /// </summary>
    public static IAircraftProfile? GetProfile(string aircraftId)
    {
        return _profiles.FirstOrDefault(p => 
            p.AircraftId.Equals(aircraftId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Détecte automatiquement le profil basé sur le titre de l'avion MSFS
    /// </summary>
    public static IAircraftProfile? DetectProfile(string aircraftTitle)
    {
        if (string.IsNullOrWhiteSpace(aircraftTitle))
            return null;

        foreach (var profile in _profiles)
        {
            foreach (var pattern in profile.DetectionPatterns)
            {
                bool match = pattern.Contains
                    ? aircraftTitle.Contains(pattern.Pattern, StringComparison.OrdinalIgnoreCase)
                    : aircraftTitle.Equals(pattern.Pattern, StringComparison.OrdinalIgnoreCase);

                if (match)
                    return profile;
            }
        }

        return null;
    }

    /// <summary>
    /// Affiche le menu de sélection de profil (mode console)
    /// </summary>
    public static IAircraftProfile? SelectProfile()
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           SÉLECTION DU PROFIL AVION                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        for (int i = 0; i < _profiles.Count; i++)
        {
            var profile = _profiles[i];
            Console.WriteLine($"  [{i + 1}] {profile.AircraftName}");
            Console.WriteLine($"      {profile.Description}");
            Console.WriteLine($"      → {profile.Commands.Count} commandes disponibles");
            Console.WriteLine();
        }

        Console.WriteLine("  [Q] Quitter");
        Console.WriteLine();
        Console.Write("Choix: ");

        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Q)
            return null;

        int index = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 0,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 1,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 2,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 3,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 4,
            _ => -1
        };

        if (index >= 0 && index < _profiles.Count)
            return _profiles[index];

        return null;
    }
}
