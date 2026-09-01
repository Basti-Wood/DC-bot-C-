namespace DCBot.Commands.Werwolf;

/// <summary>Dorfbewohner — keine Fähigkeit, nur die tägliche Abstimmung.</summary>
public sealed class VillagerRole : Role
{
    public override string Key => "villager";
    public override string DisplayName => "Dorfbewohner";
    public override string Emoji => "🧑‍🌾";
}
