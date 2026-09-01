using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Werwolf;

/// <summary>Schamane (Dorf) — schützt jede Nacht einen Spieler, aber nie zweimal denselben.</summary>
public sealed class ShamanRole : Role
{
    public override string Key => "shaman";
    public override string DisplayName => "Schamane";
    public override string Emoji => "🛡️";
    public override bool HasNightAction => true;
    public override int NightPriority => 20;

    public override string NightPrompt => "Wen möchtest du diese Nacht beschützen?";

    public override string AbilityText =>
        "Jede Nacht darfst du einen Spieler vor den Werwölfen beschützen. " +
        "Denselben Spieler kannst du nicht zweimal beschützen.";

    /// <summary>Bereits beschützte Spieler (nie zweimal derselbe).</summary>
    public HashSet<ulong> ProtectedIds { get; } = new();

    public override List<Player> GetNightTargets(WerwolfGame game, Player self)
        => game.Players.Values
            .Where(p => p.Alive && !ProtectedIds.Contains(p.User.Id))
            .OrderBy(p => p.User.Id)
            .ToList();

    public override async Task OnNightSubmittedAsync(WerwolfGame game, Player self, NightState night, SocketMessageComponent comp, ulong targetId)
    {
        await WerwolfGame.EditSourceMessageAsync(comp, m =>
        {
            m.Content = "✅ Du beschützt diese Person in dieser Nacht.";
            m.Components = new ComponentBuilder().Build();
        });
    }

    public override Task OnNightResolveAsync(WerwolfGame game, Player self, NightState night)
    {
        if (night.Selections.TryGetValue(self.User.Id, out var targetId))
        {
            ProtectedIds.Add(targetId);
            night.Protected.Add(targetId);
        }
        return Task.CompletedTask;
    }
}
