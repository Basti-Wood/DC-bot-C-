using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Werwolf;

/// <summary>Rachsüchtig (neutral) — darf beim Tod einen Spieler mitreißen und danach in den Werwolf-Chat schauen.</summary>
public sealed class VengefulRole : Role
{
    public override string Key => "vengeful";
    public override string DisplayName => "Rachsüchtiger";
    public override string Emoji => "🗡️";
    public override bool IsNeutral => true;

    public override string AbilityText =>
        "Stirbst du, darfst du einen lebenden Spieler mit in den Tod reißen. " +
        "Danach darfst du (auch wenn du kein Werwolf bist) in den Werwolf-Chat schauen.";

    /// <summary>Die Rache wurde bereits genutzt.</summary>
    public bool Used { get; private set; }

    public override async Task OnDeathAsync(WerwolfGame game, Player self)
    {
        if (Used) return;
        var targets = game.Players.Values
            .Where(t => t.Alive && t.User.Id != self.User.Id)
            .OrderBy(t => t.User.Id)
            .ToList();
        if (targets.Count == 0) return;

        var menu = game.BuildTargetMenu(game.ComponentId("vengeful"), targets, "Wen nimmst du mit in den Tod?");
        await game.TryDmAsync(self.User, new EmbedBuilder()
            .WithTitle("🗡️ Rachsüchtig")
            .WithDescription("Du stirbst — aber nicht allein! Wähle einen Spieler, der mit dir stirbt. " +
                             "Danach darfst du (als Geist) in den Werwolf-Chat schauen.")
            .WithColor(Color.DarkRed)
            .Build(),
            new ComponentBuilder().WithSelectMenu(menu).Build());
    }

    public override async Task OnDeathSelectAsync(WerwolfGame game, Player self, SocketMessageComponent comp, ulong targetId)
    {
        if (Used)
        {
            await comp.FollowupAsync("Du hast deine Rache bereits genutzt.", ephemeral: true);
            return;
        }
        if (game.Phase == GamePhase.Ended) { await comp.FollowupAsync("Das Spiel ist vorbei.", ephemeral: true); return; }
        if (!game.Players.TryGetValue(targetId, out var target) || !target.Alive || target.User.Id == self.User.Id)
        {
            await comp.FollowupAsync("Ungültiges Ziel.", ephemeral: true);
            return;
        }

        Used = true;
        await WerwolfGame.EditSourceMessageAsync(comp, m =>
        {
            m.Content = $"🗡️ Du nimmst {target.User.Mention} mit in den Tod.";
            m.Components = new ComponentBuilder().Build();
        });
        await game.SetWolfGhostAsync(self, allowRead: true);
        await game.KillAsync(target, $"wurde von {self.User.Mention} (Rachsüchtig) mit in den Tod gerissen 🗡️");
        await game.CheckWinAsync();
    }
}
