namespace DCBot.Commands.Werwolf;

/// <summary>Werwolf — stimmt nachts im Werwolf-Chat über das Opfer ab.</summary>
public sealed class WerewolfRole : Role
{
    public override string Key => "werewolf";
    public override string DisplayName => "Werwolf";
    public override string Emoji => "🐺";
    public override Team BaseTeam => Team.Werewolf;
    public override bool IsWerewolf => true;

    public override string AbilityText =>
        "Nachts stimmst du im Werwolf-Chat mit den anderen Wölfen ab, wen ihr reißt. " +
        "Tagsüber gib dich als Dorfbewohner aus!";

    /// <summary>Werwolf-Geister dürfen weiter in den Werwolf-Chat schauen (nur lesen).</summary>
    public override async Task OnDeathAsync(WerwolfGame game, Player self)
    {
        if (game.WolfChannelId is null) return;
        await game.SetWolfGhostAsync(self, allowRead: true);
    }
}
