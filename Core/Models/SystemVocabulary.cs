namespace DndBuilder.Core.Models;

/// <summary>
/// Holds system-specific terminology so the UI can relabel fields
/// (e.g. "Species" → "Ancestry" for Pathfinder 2e) without touching data.
/// Add a new entry to For() when a new game system is supported.
/// </summary>
public class SystemVocabulary
{
    // Singular forms — used for field labels and "New …" default names
    public string Species    { get; init; }
    public string Subspecies { get; init; }
    public string Class      { get; init; }
    public string Subclass   { get; init; }
    public string Ability    { get; init; }

    // Plural forms — used for sidebar accordion headers
    public string SpeciesPlural   { get; init; }
    public string ClassesPlural   { get; init; }
    public string AbilitiesPlural { get; init; }

    public static readonly SystemVocabulary Default = new()
    {
        Species         = "Species",
        Subspecies      = "Subspecies",
        Class           = "Class",
        Subclass        = "Subclass",
        Ability         = "Ability",
        SpeciesPlural   = "Species",
        ClassesPlural   = "Classes",
        AbilitiesPlural = "Abilities",
    };

    private static readonly SystemVocabulary Pathfinder2e = new()
    {
        Species         = "Ancestry",
        Subspecies      = "Heritage",
        Class           = "Class",
        Subclass        = "Archetype",
        Ability         = "Feat",
        SpeciesPlural   = "Ancestries",
        ClassesPlural   = "Classes",
        AbilitiesPlural = "Feats",
    };

    public static SystemVocabulary For(string system) => system switch
    {
        "pathfinder2e" => Pathfinder2e,
        _              => Default,
    };
}
