using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Werwolf;

/// <summary>Zwilling (Dorf) — nach dem Aufdecken der Rolle zählt die Stimme doppelt.</summary>
public sealed class TwinRole : Role
{
    public override string Key => "twin";
    public override string DisplayName => "Zwilling";
    public override string Emoji => "👯";

    public override string AbilityText =>
        "Deckst du tagsüber deine Rolle auf, zählt deine Stimme ab sofort doppelt.";

    /// <summary>Rolle wurde öffentlich aufgedeckt.</summary>
    public bool Revealed { get; private set; }

    public override int VoteWeight => Revealed ? 2 : 1;

    public override async Task OnDayStartAsync(WerwolfGame game, Player self)
    {
        if (Revealed) return;
        await game.TryDmAsync(self.User, new EmbedBuilder()
            .WithTitle("👯 Zwilling")
            .WithDescription("Du kannst deine Rolle als Zwilling aufdecken — dann zählt deine Stimme doppelt!")
            .WithColor(Color.Gold)
            .Build(),
            new ComponentBuilder()
                .WithButton("👯 Rolle aufdecken", game.ComponentId("twin"), ButtonStyle.Success)
                .Build());
    }

    public override async Task OnButtonAsync(WerwolfGame game, Player self, SocketMessageComponent comp, string action)
    {
        if (action != "twin") return;
        if (game.Phase == GamePhase.Ended) { await comp.FollowupAsync("Das Spiel ist vorbei.", ephemeral: true); return; }
        if (!self.Alive) { await comp.FollowupAsync("Du bist tot.", ephemeral: true); return; }
        if (Revealed) { await comp.FollowupAsync("Du hast dich bereits offenbart.", ephemeral: true); return; }

        Revealed = true;
        await WerwolfGame.EditSourceMessageAsync(comp, m =>
        {
            m.Content = "👯 Du hast deine Rolle offenbart — deine Stimme zählt ab jetzt doppelt!";
            m.Components = new ComponentBuilder().Build();
        });
        await game.AnnounceAsync(new EmbedBuilder()
            .WithTitle("👯 Zwilling offenbart")
            .WithDescription($"{self.User.Mention} hat sich als **Zwilling** offenbart — die Stimme zählt ab jetzt doppelt!")
            .WithColor(Color.Gold)
            .Build());
    }
}
