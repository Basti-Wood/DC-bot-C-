using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Werwolf;

/// <summary>Friendzone (neutral) — schaltet nachts einen Spieler für den nächsten Tag stumm.</summary>
public sealed class FriendzoneRole : Role
{
    public override string Key => "friendzone";
    public override string DisplayName => "Friendzone";
    public override string Emoji => "🙅";
    public override bool IsNeutral => true;
    public override bool HasNightAction => true;
    public override int NightPriority => 10;

    public override string NightPrompt => "Wen möchtest du stummschalten?";

    public override string AbilityText =>
        "Jede Nacht darfst du einen Spieler stummschalten — er darf am nächsten Tag nicht reden.";

    public override List<Player> GetNightTargets(WerwolfGame game, Player self)
        => game.Players.Values
            .Where(p => p.Alive && p.User.Id != self.User.Id)
            .OrderBy(p => p.User.Id)
            .ToList();

    public override async Task OnNightSubmittedAsync(WerwolfGame game, Player self, NightState night, SocketMessageComponent comp, ulong targetId)
    {
        await WerwolfGame.EditSourceMessageAsync(comp, m =>
        {
            m.Content = "✅ Diese Person wird für den nächsten Tag stummgeschaltet.";
            m.Components = new ComponentBuilder().Build();
        });
    }

    public override async Task OnNightResolveAsync(WerwolfGame game, Player self, NightState night)
    {
        if (!night.Selections.TryGetValue(self.User.Id, out var targetId)) return;
        if (!game.Players.TryGetValue(targetId, out var target) || !target.Alive) return;

        target.MutedForRound = true;
        await game.AnnounceAsync(new EmbedBuilder()
            .WithTitle("🙊 Stummgeschaltet")
            .WithDescription($"{target.User.Mention} wurde für diesen Tag stummgeschaltet und darf nicht reden oder abstimmen.")
            .WithColor(Color.Orange)
            .Build());
    }
}
