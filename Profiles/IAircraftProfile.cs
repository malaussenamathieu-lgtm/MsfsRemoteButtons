namespace MsfsRemoteButtons.Profiles;

/// <summary>
/// Type de contrôle pour l'affichage web
/// </summary>
public enum ControlType
{
    Toggle,      // Bouton ON/OFF simple
    Selector,    // Sélecteur multi-positions (ex: magnetos)
    Momentary    // Bouton poussoir (appui momentané)
}

/// <summary>
/// Option pour les sélecteurs multi-positions
/// </summary>
public class SelectorOption
{
    public string Label { get; set; } = "";
    public string SimEvent { get; set; } = "";
    public int Value { get; set; }
}

/// <summary>
/// Représente une commande disponible pour un avion
/// </summary>
public class AircraftCommand
{
    public string Id { get; set; } = "";               // Identifiant unique
    public string Name { get; set; } = "";             // Nom affiché
    public string SimEvent { get; set; } = "";         // K:Event SimConnect pour toggle
    public string? SimEventOn { get; set; }            // K:Event pour "allumer" (optionnel)
    public string? SimEventOff { get; set; }           // K:Event pour "éteindre" (optionnel)
    public string? SimVar { get; set; }                // Variable SimConnect pour lire l'état
    public string? SimVarUnit { get; set; } = "Bool";  // Unité de la SimVar
    public string Category { get; set; } = "";         // Catégorie (Lights, Electrical, etc.)
    public ConsoleKey Key { get; set; }                // Touche clavier (pour mode console)
    public string KeyDisplay { get; set; } = "";       // Affichage de la touche
    public ControlType ControlType { get; set; } = ControlType.Toggle;
    public List<SelectorOption>? SelectorOptions { get; set; }  // Options pour les sélecteurs
    public bool Hidden { get; set; } = false;          // Cacher de l'interface (lecture état uniquement)
    public bool IsMomentary { get; set; } = false;     // Press+release au lieu d'un seul press
}

/// <summary>
/// Pattern de détection d'avion
/// </summary>
public class AircraftPattern
{
    public string Pattern { get; set; } = "";      // Texte à chercher dans le TITLE
    public bool Contains { get; set; } = true;     // true = contains, false = exact match
}

/// <summary>
/// Interface pour tous les profils d'avion
/// </summary>
public interface IAircraftProfile
{
    string AircraftName { get; }                       // Nom de l'avion
    string AircraftId { get; }                         // ID pour la sélection
    string Description { get; }                        // Description
    List<AircraftPattern> DetectionPatterns { get; }   // Patterns pour auto-détection
    List<string> Categories { get; }                   // Catégories de commandes
    List<AircraftCommand> Commands { get; }

    /// <summary>
    /// Exporte les SimEvents du profil dans un fichier texte
    /// </summary>
    void ExportSimEventsToFile(string? outputDirectory = null);
}
