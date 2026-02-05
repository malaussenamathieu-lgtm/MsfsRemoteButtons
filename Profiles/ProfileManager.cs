using System.Text.RegularExpressions;

namespace MsfsRemoteButtons.Profiles;

// ============================================================================
// GESTIONNAIRE DE PROFILS
// ============================================================================
//
// Ce module gère la collection de profils d'avion disponibles et la détection
// automatique du profil approprié basée sur le titre de l'avion dans MSFS
// (SimVar TITLE).
//
// Détection améliorée:
// - Le titre est normalisé (Trim + espaces multiples réduits à un seul)
// - Les patterns sont évalués dans l'ordre: le premier match gagne
// - Patterns les plus spécifiques en premier (ex: "Pilatus PC-12 NGX" avant "PC-12")
//
// Pour ajouter un nouvel avion:
// 1. Créer une classe qui implémente IAircraftProfile (voir Cessna172G1000Profile)
// 2. L'ajouter dans la liste _profiles ci-dessous (ordre = priorité de détection)
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
        new PilatusPC12Profile(),
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
        string? normalized = NormalizeAircraftTitle(aircraftTitle);
        if (string.IsNullOrEmpty(normalized))
            return null;

        foreach (var profile in _profiles)
        {
            foreach (var pattern in profile.DetectionPatterns)
            {
                if (string.IsNullOrEmpty(pattern.Pattern)) continue;

                // Contains = substring match, sinon exact match
                bool match = pattern.Contains
                    ? normalized.Contains(pattern.Pattern, StringComparison.OrdinalIgnoreCase)
                    : normalized.Equals(pattern.Pattern, StringComparison.OrdinalIgnoreCase);

                if (match)
                    return profile;
            }
        }

        return null;  // Aucun profil trouvé - SimConnectService utilisera DefaultProfile
    }

    /// <summary>
    /// Normalise le titre avion pour une détection fiable (Trim + espaces multiples réduits).
    /// </summary>
    public static string? NormalizeAircraftTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var trimmed = title.Trim();
        if (trimmed.Length == 0) return null;
        return Regex.Replace(trimmed, @"\s+", " ");
    }

    // Méthode SelectProfile supprimée - sélection manuelle non utilisée (détection automatique uniquement)
}
