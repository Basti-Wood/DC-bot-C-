using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Setup;

/// <summary>
/// /setup rules — öffnet das Regel-Admin-Modal. Der Submit wird vom
/// Event-Listener Events/RulesSetup/RulesModalEvent.cs verarbeitet.
/// </summary>
public sealed class SetupCommand : ICommand
{
    public const string SetRuleModalId = "set_rule_modal";

    public string Name => "setup";
    public string Description => "Server-Setup Befehle";
    public bool AllowAdmin => true;

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("rules")
            .WithDescription("Erstelle das Regelwerk")
            .WithType(ApplicationCommandOptionType.SubCommand),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        var sub = cmd.Data.Options.FirstOrDefault();
        if (sub?.Name != "rules")
        {
            await cmd.RespondAsync("Unbekannter Unterbefehl.", ephemeral: true);
            return;
        }

        var modal = new ModalBuilder()
            .WithTitle("Regel Admin Interface")
            .WithCustomId(SetRuleModalId)
            .AddTextInput(
                "Setzt die Regel (leer = Standard)",
                "rule",
                TextInputStyle.Paragraph,
                placeholder: "Leer lassen um die Standard-Regeln zu verwenden.",
                required: false)
            .Build();

        await cmd.RespondWithModalAsync(modal);
    }
}
