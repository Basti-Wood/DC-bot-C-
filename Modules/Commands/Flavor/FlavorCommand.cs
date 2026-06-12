using DCBot.Core;
using DCBot.Services.Booster;
using DCBot.Services.Flavor;
using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Flavor;

/// <summary>
/// /flavor — Server-Flavourtexte und Booster-Namen anpassen.
/// </summary>
public sealed class FlavorCommand : ICommand
{
    public string Name => "flavor";
    public string Description => "Customize stylized texts and labels";
    public bool AllowAdmin => true;

    private readonly FlavorService _flavor;
    private readonly BoosterService _booster;

    public FlavorCommand(FlavorService flavor, BoosterService booster)
    {
        _flavor = flavor;
        _booster = booster;
    }

    public List<SlashCommandOptionBuilder> BuildOptions()
    {
        var counter = new SlashCommandOptionBuilder()
            .WithName("counter")
            .WithDescription("Setzt Counter-Channel-Text (nutze {count} als Platzhalter)")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("text")
                .WithDescription("Beispiel: 「👥」Kinder✩{count}")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true));

        var boosterKey = new SlashCommandOptionBuilder()
            .WithName("role")
            .WithDescription("Welche Booster-Auswahl soll umbenannt werden")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        foreach (var roleKey in BoosterService.SelectableRoleKeys)
            boosterKey.AddChoice(BoosterService.GetDefaultRoleLabel(roleKey), roleKey);

        var booster = new SlashCommandOptionBuilder()
            .WithName("booster")
            .WithDescription("Setzt die sichtbare Bezeichnung einer Booster-Auswahl")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(boosterKey)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("text")
                .WithDescription("Neue sichtbare Bezeichnung")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true));

        var show = new SlashCommandOptionBuilder()
            .WithName("show")
            .WithDescription("Zeigt aktuelle Flavor-Einstellungen")
            .WithType(ApplicationCommandOptionType.SubCommand);

        return new List<SlashCommandOptionBuilder> { counter, booster, show };
    }

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        if (cmd.GuildId is not { } guildId)
        {
            await cmd.RespondAsync("Dieser Befehl funktioniert nur auf einem Server.", ephemeral: true);
            return;
        }

        var sub = cmd.Data.Options.FirstOrDefault();

        switch (sub?.Name)
        {
            case "counter":
            {
                var template = ((string)sub.Options.First(o => o.Name == "text").Value).Trim();
                if (string.IsNullOrWhiteSpace(template))
                {
                    await cmd.RespondAsync("Bitte gib einen nicht-leeren Text an.", ephemeral: true);
                    return;
                }

                await _flavor.SetCounterTemplateAsync(guildId, template);
                var preview = FlavorService.RenderCounterName(template, 123);
                await cmd.RespondAsync(
                    $"Counter-Flavor gespeichert. Vorschau: `{preview}`",
                    ephemeral: true);
                return;
            }

            case "booster":
            {
                var roleKey = (string)sub.Options.First(o => o.Name == "role").Value;
                var label = ((string)sub.Options.First(o => o.Name == "text").Value).Trim();

                if (string.IsNullOrWhiteSpace(label))
                {
                    await cmd.RespondAsync("Bitte gib einen nicht-leeren Text an.", ephemeral: true);
                    return;
                }

                await _booster.SetRoleLabelAsync(guildId, roleKey, label);
                await cmd.RespondAsync($"Booster-Flavor gespeichert: `{roleKey}` -> `{label}`", ephemeral: true);
                return;
            }

            case "show":
            {
                var counterTemplate = await _flavor.GetCounterTemplateAsync(guildId);
                var labels = await _booster.GetRoleLabelsAsync(guildId);

                var embed = new EmbedBuilder()
                    .WithTitle("Flavor Einstellungen")
                    .WithColor(new Color(0x3498db))
                    .AddField("Counter Template", counterTemplate)
                    .AddField("Counter Vorschau", FlavorService.RenderCounterName(counterTemplate, 123));

                foreach (var roleKey in BoosterService.SelectableRoleKeys)
                {
                    var label = labels.GetValueOrDefault(roleKey, BoosterService.GetDefaultRoleLabel(roleKey));
                    embed.AddField(roleKey, label, inline: true);
                }

                await cmd.RespondAsync(embed: embed.Build(), ephemeral: true);
                return;
            }

            default:
                await cmd.RespondAsync("Unbekannter Unterbefehl.", ephemeral: true);
                return;
        }
    }
}
