using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Cookie;

/// <summary>/cookie user — verschenkt einen Cookie 🍪 (Port von cookie_command.go).</summary>
public sealed class CookieCommand : ICommand
{
    public string Name => "cookie";
    public string Description => "Verschenke einen Cookie 🍪 an jemanden";
    public bool AllowEveryone => true;
    public int Cooldown => 5;

    private static readonly string[] Messages =
    {
        "Ohaa {0}, du hast einen Cookie 🍪 von {1} bekommen :D. Wie toll!",
        "Einen Moment... {0}, du hast einen Cookie 🍪 von {1} bekommen?!?!?",
        "Eyyy {0}! {1} hat dir einen unglaublich leckeren Cookie 🍪 geschenkt!",
        "Ohh sieh mal {0}, {1} schenkt dir einen Cookie 🍪!",
        "Yooo {0}!!! {1} hat einen Cookie 🍪 aus der Dose geklaut für dich!!!",
        "{1} wirft {0} einen Cookie 🍪 an den Kopf. Treffer!",
    };

    private readonly Random _random = new();

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("user")
            .WithDescription("Wer bekommt den Cookie?")
            .WithType(ApplicationCommandOptionType.User)
            .WithRequired(true),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        if (cmd.Data.Options.FirstOrDefault(o => o.Name == "user")?.Value is not IUser target)
        {
            await cmd.RespondAsync("Bitte gib einen Benutzer an.", ephemeral: true);
            return;
        }

        var text = Messages[_random.Next(Messages.Length)];
        await cmd.RespondAsync(string.Format(text, target.Mention, cmd.User.Mention));
    }
}
