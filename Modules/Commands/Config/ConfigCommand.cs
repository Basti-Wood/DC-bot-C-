using DCBot.Core;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Config;

/// <summary>/config setchannel — Bot-/Welcome-/Main-/Log-Channel festlegen (Admin).</summary>
public sealed class ConfigCommand : ICommand
{
    public string Name => "config";
    public string Description => "Guild-spezifische Konfiguration";
    public bool AllowAdmin => true;

    private static readonly (string Name, string Value)[] ChannelChoices =
    {
        ("Bot Channel", "bot"),
        ("Welcome Channel", "welcome"),
        ("Main Channel", "main"),
        ("Logs Channel", "logs"),
    };

    private readonly GuildConfigService _guildConfig;
    private readonly ILogger<ConfigCommand> _logger;

    public ConfigCommand(GuildConfigService guildConfig, ILogger<ConfigCommand> logger)
    {
        _guildConfig = guildConfig;
        _logger = logger;
    }

    public List<SlashCommandOptionBuilder> BuildOptions()
    {
        var whichOption = new SlashCommandOptionBuilder()
            .WithName("which")
            .WithDescription("Welche Channel-Konfiguration gesetzt werden soll")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);
        foreach (var (name, value) in ChannelChoices)
            whichOption.AddChoice(name, value);

        return new List<SlashCommandOptionBuilder>
        {
            new SlashCommandOptionBuilder()
                .WithName("setchannel")
                .WithDescription("Einen konfigurierten Channel für diese Guild setzen")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(whichOption)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("channel")
                    .WithDescription("Channel für die gewählte Konfiguration")
                    .WithType(ApplicationCommandOptionType.Channel)
                    .WithRequired(true)),
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
        if (sub?.Name != "setchannel")
        {
            await cmd.RespondAsync("Unbekannter Unterbefehl.", ephemeral: true);
            return;
        }

        var which = (string)sub.Options.First(o => o.Name == "which").Value;
        var channel = (IChannel)sub.Options.First(o => o.Name == "channel").Value;

        await _guildConfig.SetAsync(guildId, $"channel:{which}", channel.Id.ToString());

        _logger.LogInformation("config: channel:{Which} = {Channel} for guild {Guild}",
            which, channel.Id, guildId);

        await cmd.RespondAsync(
            $"Konfiguration gespeichert: `{which}` → <#{channel.Id}>", ephemeral: true);
    }
}
