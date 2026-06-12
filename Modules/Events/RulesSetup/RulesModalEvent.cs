using System.Text.RegularExpressions;
using DCBot.Commands.Setup;
using DCBot.Services.Misc;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.RulesSetup;

/// <summary>
/// Verarbeitet das /setup-rules-Modal: leerer Input → Standard-Regeln,
/// sonst eigener Text (§N wird automatisch gefettet).
/// </summary>
public sealed partial class RulesModalEvent : IEventListener
{
    public string Name => "RulesSetup";

    private readonly ILogger<RulesModalEvent> _logger;

    public RulesModalEvent(ILogger<RulesModalEvent> logger)
    {
        _logger = logger;
    }

    public void Register(EventManager events)
    {
        events.OnModalSubmitted(HandleAsync);
    }

    [GeneratedRegex(@"(§\d+)")]
    private static partial Regex ParagraphRegex();

    private async Task HandleAsync(SocketModal modal)
    {
        if (modal.Data.CustomId != SetupCommand.SetRuleModalId) return;

        // Nur Admins dürfen das Regelwerk setzen
        if (modal.User is not SocketGuildUser { GuildPermissions.Administrator: true })
        {
            await modal.RespondAsync("Du hast keine Berechtigung dafür.", ephemeral: true);
            return;
        }

        var ruleContent = modal.Data.Components
            .FirstOrDefault(c => c.CustomId == "rule")?.Value?.Trim() ?? "";

        if (ruleContent.Length == 0)
            ruleContent = Rules.DefaultRuleText();
        else
            ruleContent = ParagraphRegex().Replace(ruleContent, "**$1**");

        var embed = new EmbedBuilder()
            .WithTitle("📜 Regelwerk")
            .WithDescription(ruleContent)
            .WithColor(new Color(0x9b59b6))
            .Build();

        await modal.Channel.SendMessageAsync(embed: embed);

        _logger.LogInformation("Rulebook set in channel {Channel} by {User}",
            modal.Channel.Id, modal.User.Username);

        await modal.RespondAsync("Regelwerk wurde gesendet. ✅", ephemeral: true);
    }
}
