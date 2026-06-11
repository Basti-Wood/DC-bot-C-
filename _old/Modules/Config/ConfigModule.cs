using DCBot.Core;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Modules.Config;

/// <summary>
/// Guild configuration module: /config setchannel ...
/// Stores per-guild settings in the guild_configs table
/// (port of the Go bot's config module, trimmed to channels).
/// </summary>
public sealed class ConfigModule : IModule
{
    public string Name => "Config";

    private static readonly (string Name, string Value)[] ChannelChoices =
    {
        ("Bot Channel", "bot"),
        ("Welcome Channel", "welcome"),
        ("Main Channel", "main"),
        ("Logs Channel", "logs"),
    };

    private readonly GuildConfigService _guildConfig;
    private readonly ILogger<ConfigModule> _logger;

    public ConfigModule(GuildConfigService guildConfig, ILogger<ConfigModule> logger)
    {
        _guildConfig = guildConfig;
        _logger = logger;
    }

    public void Register(CommandManager commands, EventManager events)
    {
        var whichOption = new SlashCommandOptionBuilder()
            .WithName("which")
            .WithDescription("Which channel config to set")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);
        foreach (var (name, value) in ChannelChoices)
            whichOption.AddChoice(name, value);

        var setChannel = new SlashCommandOptionBuilder()
            .WithName("setchannel")
            .WithDescription("Set a configured channel for this guild")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(whichOption)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("channel")
                .WithDescription("Channel to use for the selected config")
                .WithType(ApplicationCommandOptionType.Channel)
                .WithRequired(true));

        commands.Register(new SlashCommand
        {
            Name = "config",
            Description = "Guild-specific configuration",
            AllowAdmin = true,
            Options = { setChannel },
            Handler = ConfigCommandAsync,
        });
    }

    private async Task ConfigCommandAsync(SocketSlashCommand cmd)
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
