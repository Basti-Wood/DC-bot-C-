using DCBot.Core;
using DCBot.Services.Booster;
using DCBot.Services.Flavor;
using DCBot.Services.Misc;
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
    private readonly TextAssetService _textAssets;

    public FlavorCommand(FlavorService flavor, BoosterService booster, TextAssetService textAssets)
    {
        _flavor = flavor;
        _booster = booster;
        _textAssets = textAssets;
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

        var ruleText = new SlashCommandOptionBuilder()
            .WithName("rule_text")
            .WithDescription("Setzt den Text einer Regel aus assets/text/rules.json")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(BuildRuleChoiceOption())
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("text")
                .WithDescription("Neuer Regeltext")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true));

        var ruleLabel = new SlashCommandOptionBuilder()
            .WithName("rule_label")
            .WithDescription("Setzt das Auswahl-Label einer Regel")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(BuildRuleChoiceOption())
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("label")
                .WithDescription("Neues Label im /rule Command")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true));

        var goodnightSet = new SlashCommandOptionBuilder()
            .WithName("goodnight_set")
            .WithDescription("Ersetzt eine Nachricht aus assets/text/GN.json")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("index")
                .WithDescription("1-basierter Index der Nachricht")
                .WithType(ApplicationCommandOptionType.Integer)
                .WithRequired(true)
                .WithMinValue(1))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("text")
                .WithDescription("Neue Nachricht")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true));

        var goodnightAdd = new SlashCommandOptionBuilder()
            .WithName("goodnight_add")
            .WithDescription("Fügt eine Nachricht zu assets/text/GN.json hinzu")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("text")
                .WithDescription("Neue Nachricht")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true));

        var goodnightRemove = new SlashCommandOptionBuilder()
            .WithName("goodnight_remove")
            .WithDescription("Entfernt eine Nachricht aus assets/text/GN.json")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("index")
                .WithDescription("1-basierter Index der Nachricht")
                .WithType(ApplicationCommandOptionType.Integer)
                .WithRequired(true)
                .WithMinValue(1));

        var welcomeSet = new SlashCommandOptionBuilder()
            .WithName("welcome_set")
            .WithDescription("Setzt einen Textbaustein aus assets/text/welcome.json")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(BuildWelcomeFieldOption())
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("text")
                .WithDescription("Neuer Text mit Platzhaltern")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(true));

        return new List<SlashCommandOptionBuilder>
        {
            counter,
            booster,
            ruleText,
            ruleLabel,
            goodnightSet,
            goodnightAdd,
            goodnightRemove,
            welcomeSet,
            show,
        };
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

            case "rule_text":
            {
                var ruleId = (string)sub.Options.First(o => o.Name == "rule").Value;
                var text = ((string)sub.Options.First(o => o.Name == "text").Value).Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    await cmd.RespondAsync("Bitte gib einen nicht-leeren Regeltext an.", ephemeral: true);
                    return;
                }

                var updated = await _textAssets.UpdateRuleTextAsync(ruleId, text);
                await cmd.RespondAsync(
                    updated ? $"Regeltext {ruleId} gespeichert." : "Regel nicht gefunden.",
                    ephemeral: true);
                return;
            }

            case "rule_label":
            {
                var ruleId = (string)sub.Options.First(o => o.Name == "rule").Value;
                var label = ((string)sub.Options.First(o => o.Name == "label").Value).Trim();

                if (string.IsNullOrWhiteSpace(label))
                {
                    await cmd.RespondAsync("Bitte gib ein nicht-leeres Label an.", ephemeral: true);
                    return;
                }

                var updated = await _textAssets.UpdateRuleLabelAsync(ruleId, label);
                await cmd.RespondAsync(
                    updated ? $"Regel-Label {ruleId} gespeichert." : "Regel nicht gefunden.",
                    ephemeral: true);
                return;
            }

            case "goodnight_set":
            {
                var index = Convert.ToInt32(sub.Options.First(o => o.Name == "index").Value);
                var text = ((string)sub.Options.First(o => o.Name == "text").Value).Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    await cmd.RespondAsync("Bitte gib eine nicht-leere Nachricht an.", ephemeral: true);
                    return;
                }

                var updated = await _textAssets.UpdateGoodnightMessageAsync(index, text);
                await cmd.RespondAsync(
                    updated ? $"Goodnight-Nachricht {index} gespeichert." : "Ungültiger Nachricht-Index.",
                    ephemeral: true);
                return;
            }

            case "goodnight_add":
            {
                var text = ((string)sub.Options.First(o => o.Name == "text").Value).Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    await cmd.RespondAsync("Bitte gib eine nicht-leere Nachricht an.", ephemeral: true);
                    return;
                }

                await _textAssets.AddGoodnightMessageAsync(text);
                await cmd.RespondAsync("Goodnight-Nachricht hinzugefügt.", ephemeral: true);
                return;
            }

            case "goodnight_remove":
            {
                var index = Convert.ToInt32(sub.Options.First(o => o.Name == "index").Value);
                var removed = await _textAssets.RemoveGoodnightMessageAsync(index);
                await cmd.RespondAsync(
                    removed ? $"Goodnight-Nachricht {index} entfernt." : "Ungültiger Nachricht-Index.",
                    ephemeral: true);
                return;
            }

            case "welcome_set":
            {
                var field = (string)sub.Options.First(o => o.Name == "field").Value;
                var text = ((string)sub.Options.First(o => o.Name == "text").Value).Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    await cmd.RespondAsync("Bitte gib einen nicht-leeren Text an.", ephemeral: true);
                    return;
                }

                var updated = await _textAssets.UpdateWelcomeFieldAsync(field, text);
                await cmd.RespondAsync(
                    updated ? $"Welcome-Text `{field}` gespeichert." : "Unbekanntes Welcome-Feld.",
                    ephemeral: true);
                return;
            }

            case "show":
            {
                var counterTemplate = await _flavor.GetCounterTemplateAsync(guildId);
                var labels = await _booster.GetRoleLabelsAsync(guildId);
                var rules = _textAssets.GetRules();
                var goodnight = _textAssets.GetGoodnight();
                var welcome = _textAssets.GetWelcome();

                var embed = new EmbedBuilder()
                    .WithTitle("Flavor Einstellungen")
                    .WithColor(new Color(0x3498db))
                    .AddField("Counter Template", counterTemplate)
                    .AddField("Counter Vorschau", FlavorService.RenderCounterName(counterTemplate, 123));

                embed.AddField(
                    "Rules",
                    string.Join("\n", rules.Rules.Select(rule => $"{rule.Id}: {TextAssetService.Summarize(rule.Label, 60)}")));

                embed.AddField(
                    "Goodnight",
                    goodnight.Messages.Count == 0
                        ? "Keine Nachrichten konfiguriert"
                        : string.Join("\n", goodnight.Messages.Select((message, index) => $"{index + 1}. {TextAssetService.Summarize(message, 70)}")));

                embed.AddField("Welcome Titel", TextAssetService.Summarize(welcome.EmbedTitle, 120));
                embed.AddField("Welcome Main", TextAssetService.Summarize(welcome.MainAnnouncement, 120));

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

    private SlashCommandOptionBuilder BuildRuleChoiceOption()
    {
        var option = new SlashCommandOptionBuilder()
            .WithName("rule")
            .WithDescription("Welche Regel soll bearbeitet werden")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        foreach (var (label, value) in _textAssets.GetRuleChoices())
            option.AddChoice(label, value);

        return option;
    }

    private static SlashCommandOptionBuilder BuildWelcomeFieldOption()
        => new SlashCommandOptionBuilder()
            .WithName("field")
            .WithDescription("Welcher Welcome-Text soll bearbeitet werden")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true)
            .AddChoice("DM bei zu jungem Account", "underage_dm")
            .AddChoice("Embed Titel", "embed_title")
            .AddChoice("Embed Beschreibung mit Main-Channel", "embed_description_with_main")
            .AddChoice("Embed Beschreibung ohne Main-Channel", "embed_description_without_main")
            .AddChoice("Embed Footer", "embed_footer")
            .AddChoice("Main-Channel Ankündigung", "main_announcement");
}
