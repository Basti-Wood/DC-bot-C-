using DCBot.Services.Misc;
using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Rule;

/// <summary>/rule — schickt eine bestimmte Regel anonym in den Chat.</summary>
public sealed class RuleCommand : ICommand
{
    public string Name => "rule";
    public string Description => "Schicke eine bestimmte Regel in den Chat (Es ist Anonym!)";
    public bool AllowEveryone => true;

    public List<SlashCommandOptionBuilder> BuildOptions()
    {
        var option = new SlashCommandOptionBuilder()
            .WithName("rule")
            .WithDescription("Wähle eine Regel")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);
        foreach (var (name, value) in Rules.Choices)
            option.AddChoice(name, value);
        return new List<SlashCommandOptionBuilder> { option };
    }

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        var ruleValue = (string)cmd.Data.Options.First(o => o.Name == "rule").Value;

        if (!Rules.Texts.TryGetValue(ruleValue, out var ruleText))
        {
            await cmd.RespondAsync("Unbekannte Regel.", ephemeral: true);
            return;
        }

        // Anonym: Regel als normale Channel-Nachricht, Bestätigung ephemeral
        var embed = new EmbedBuilder()
            .WithDescription(ruleText)
            .WithColor(new Color(0x9b59b6))
            .Build();

        await cmd.Channel.SendMessageAsync(embed: embed);
        await cmd.RespondAsync("Regel wurde gesendet. ✅", ephemeral: true);
    }
}
