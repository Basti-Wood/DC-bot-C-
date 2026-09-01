using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Werwolf;

/// <summary>Zauberer (Dorf) — sieht jede Nacht die Gesinnung eines Spielers.</summary>
public sealed class WizardRole : Role
{
    public override string Key => "wizard";
    public override string DisplayName => "Zauberer";
    public override string Emoji => "🔮";
    public override bool HasNightAction => true;
    public override int NightPriority => 30;

    public override string NightPrompt => "Wen möchtest du diese Nacht prüfen?";

    public override string AbilityText =>
        "Jede Nacht darfst du die Gesinnung (Dorf oder Werwolf) eines anderen Spielers sehen.";

    public override List<Player> GetNightTargets(WerwolfGame game, Player self)
        => game.Players.Values
            .Where(p => p.Alive && p.User.Id != self.User.Id)
            .OrderBy(p => p.User.Id)
            .ToList();

    public override async Task OnNightSubmittedAsync(WerwolfGame game, Player self, NightState night, SocketMessageComponent comp, ulong targetId)
    {
        await WerwolfGame.EditSourceMessageAsync(comp, m =>
        {
            m.Content = "✅ Du schaust in deine Kristallkugel ... Das Ergebnis kommt am Morgen.";
            m.Components = new ComponentBuilder().Build();
        });
    }

    public override async Task OnNightResolveAsync(WerwolfGame game, Player self, NightState night)
    {
        if (!night.Selections.TryGetValue(self.User.Id, out var targetId)) return;
        if (!game.Players.TryGetValue(targetId, out var target)) return;

        var team = game.GetTeam(target) == Team.Werewolf ? "**Werwolf** 🐺" : "**Dorf** 🏡";
        await game.TryDmAsync(self.User, new EmbedBuilder()
            .WithTitle("🔮 Ergebnis deiner Prüfung")
            .WithDescription($"{target.User.Mention} gehört zum Lager: {team}")
            .WithColor(Color.Purple)
            .Build());
    }
}
