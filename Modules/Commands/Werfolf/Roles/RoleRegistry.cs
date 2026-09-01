namespace DCBot.Commands.Werwolf;

/// <summary>
/// Registriert alle Bonusrollen, die der Host aktivieren kann.
/// Neue Rolle hinzufügen = hier einen Eintrag ergänzen (+ neue Rollenklasse).
/// </summary>
public static class RoleRegistry
{
    private static readonly Dictionary<string, Func<Role>> Factories = new()
    {
        ["wizard"] = () => new WizardRole(),
        ["shaman"] = () => new ShamanRole(),
        ["vengeful"] = () => new VengefulRole(),
        ["twin"] = () => new TwinRole(),
        ["friendzone"] = () => new FriendzoneRole(),
        ["cupid"] = () => new CupidRole(),
    };

    /// <summary>Alle zuschaltbaren Bonusrollen in Anzeige-Reihenfolge.</summary>
    public static IReadOnlyList<(string Key, string Label)> BonusRoles { get; } =
    [
        ("wizard", "Zauberer"),
        ("shaman", "Schamane"),
        ("vengeful", "Rachsüchtig"),
        ("twin", "Zwilling"),
        ("friendzone", "Friendzone"),
        ("cupid", "Amor"),
    ];

    public static Role Create(string key) => Factories[key]();

    public static string LabelOf(string key)
        => BonusRoles.FirstOrDefault(r => r.Key == key).Label;
}
