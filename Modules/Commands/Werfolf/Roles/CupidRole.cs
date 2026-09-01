using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Werwolf;

/// <summary>Amor (Dorf) — verkuppelt zu Spielbeginn zwei Spieler; stirbt einer, stirbt der andere.</summary>
public sealed class CupidRole : Role
{
    public override string Key => "cupid";
    public override string DisplayName => "Amor";
    public override string Emoji => "💘";
    public override bool HasGameStartAction => true;

    public override string AbilityText =>
        "Zu Spielbeginn verkuppelst du zwei Spieler. Stirbt einer von ihnen, stirbt der andere an Liebeskummer.";

    private ulong? _first;

    public override async Task OnGameStartAsync(WerwolfGame game, Player self)
    {
        var others = game.Players.Values
            .Where(p => p.Alive && p.User.Id != self.User.Id)
            .OrderBy(p => p.User.Id)
            .ToList();

        var embed = new EmbedBuilder()
            .WithTitle("💘 Amor")
            .WithDescription("Wähle zwei Spieler, die sich verlieben.\nStirbt einer von beiden, stirbt der andere an Liebeskummer.")
            .WithColor(Color.Magenta)
            .Build();

        var menuA = game.BuildTargetMenu(game.ComponentId("cupid", "a"), others, "Wähle den ersten Verliebten");
        var menuB = game.BuildTargetMenu(game.ComponentId("cupid", "b"), others, "Wähle den zweiten Verliebten");
        var ok = await game.TryDmAsync(self.User, embed,
            new ComponentBuilder().WithSelectMenu(menuA, 0).WithSelectMenu(menuB, 1).Build());

        if (!ok)
        {
            await game.BeginNightAsync(1);
            return;
        }

        await game.AnnounceAsync(new EmbedBuilder()
            .WithTitle("💘 Amor wählt ...")
            .WithDescription("Amor wählt gerade zwei Verliebte aus. Der Host kann diesen Schritt überspringen.")
            .WithColor(Color.Magenta)
            .Build(),
            new ComponentBuilder()
                .WithButton("Überspringen (Host)", game.ComponentId("force", "cupid"), ButtonStyle.Danger)
                .Build());
    }

    public override async Task OnSetupSelectAsync(WerwolfGame game, Player self, SocketMessageComponent comp, string step, ulong value)
    {
        if (game.Phase != GamePhase.Cupid) { await comp.FollowupAsync("Die Verliebten wurden bereits gewählt.", ephemeral: true); return; }
        if (!game.Players.TryGetValue(value, out var target) || !target.Alive || target.User.Id == self.User.Id)
        {
            await comp.FollowupAsync("Ungültiges Ziel.", ephemeral: true);
            return;
        }

        if (step != "b" || _first is null)
        {
            _first = value;
            await WerwolfGame.EditSourceMessageAsync(comp, m => m.Content = $"💘 Partner 1: {target.User.Mention}\nWähle jetzt Partner 2.");
            return;
        }

        if (_first == value)
        {
            await comp.FollowupAsync("Bitte wähle zwei verschiedene Spieler.", ephemeral: true);
            return;
        }

        var a = game.Players[_first.Value];
        var b = target;
        a.Lover = b;
        b.Lover = a;

        await WerwolfGame.EditSourceMessageAsync(comp, m =>
        {
            m.Content = $"💘 {a.User.Mention} und {b.User.Mention} sind jetzt verliebt!";
            m.Components = new ComponentBuilder().Build();
        });

        await game.TryDmAsync(a.User, LoverEmbed(b));
        await game.TryDmAsync(b.User, LoverEmbed(a));

        await game.AnnounceAsync(new EmbedBuilder()
            .WithTitle("💘 Zwei Herzen verbunden")
            .WithDescription($"{a.User.Mention} und {b.User.Mention} sind jetzt ein Paar! Stirbt einer, stirbt der andere an Liebeskummer.")
            .WithColor(Color.Magenta)
            .Build());

        await game.BeginNightAsync(1);
    }

    private static Embed LoverEmbed(Player other) => new EmbedBuilder()
        .WithTitle("💘 Du bist verliebt!")
        .WithDescription($"Du bist jetzt mit **{other.User.DisplayName}** verliebt.\n" +
                         $"Stirbt {other.User.DisplayName}, stirbst du an gebrochenem Herzen — du wirst benachrichtigt.")
        .WithColor(Color.Magenta)
        .Build();
}
