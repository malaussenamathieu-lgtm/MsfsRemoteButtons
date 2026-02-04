namespace MsfsRemoteButtons.Profiles;

// ============================================================================
// GESTIONNAIRE DE PROFILS
// ============================================================================
//
// Ce module gère la collection de profils d'avion disponibles et la détection
// automatique du profil approprié basée sur le titre de l'avion dans MSFS.
//
// Pour ajouter un nouvel avion:
// 1. Créer une classe qui implémente IAircraftProfile (voir Cessna172G1000Profile)
// 2. L'ajouter dans la liste _profiles ci-dessous
// 3. Définir des DetectionPatterns pour la détection automatique
//
// ============================================================================

/// <summary>
/// Gestionnaire des profils d'avion disponibles
/// Responsable de:
/// - Stocker la liste des profils supportés
/// - Détecter automatiquement le profil selon le titre MSFS
/// - Fournir un profil par défaut si aucune correspondance
/// </summary>
public static class ProfileManager
{
    // Liste des profils disponibles - Ordre important pour la détection
    // Le premier profil avec un pattern qui matche sera utilisé
    private static readonly List<IAircraftProfile> _profiles = new()
    {
        new Cessna172G1000Profile(),
        // ----------------------------------------
        // AJOUTER D'AUTRES PROFILS ICI:
        // ----------------------------------------
        // new Boeing737Profile(),
        // new A320NeoProfile(),
        // new TBM930Profile(),
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
    ///
    /// Algorithme:
    /// 1. Parcourt tous les profils dans l'ordre de _profiles
    /// 2. Pour chaque profil, vérifie ses DetectionPatterns
    /// 3. Retourne le premier profil dont un pattern correspond
    ///
    /// Exemples de titres MSFS:
    /// - "Cessna 172 Skyhawk G1000 Asobo" → Cessna172G1000Profile
    /// - "Boeing 737-800" → Boeing737Profile (si défini)
    /// </summary>
    /// <param name="aircraftTitle">Titre de l'avion depuis SimVar TITLE</param>
    /// <returns>Le profil correspondant ou null si aucun match</returns>
    public static IAircraftProfile? DetectProfile(string aircraftTitle)
    {
        if (string.IsNullOrWhiteSpace(aircraftTitle))
            return null;

        foreach (var profile in _profiles)
        {
            foreach (var pattern in profile.DetectionPatterns)
            {
                // Contains = substring match, sinon exact match
                bool match = pattern.Contains
                    ? aircraftTitle.Contains(pattern.Pattern, StringComparison.OrdinalIgnoreCase)
                    : aircraftTitle.Equals(pattern.Pattern, StringComparison.OrdinalIgnoreCase);

                if (match)
                    return profile;
            }
        }

        return null;  // Aucun profil trouvé - SimConnectService utilisera DefaultProfile
    }

    /// <summary>
    /// Affiche le menu de sélection de profil (mode console)
    /// Note: Non utilisé actuellement car la détection est automatique
    /// Conservé pour le debug ou une future sélection manuelle
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
