using DCBot.Core;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Config;

/// <summary>
/// /config — setchannel / setrole / setlevelrole (Port des Go config-Moduls).
/// </summary>
public sealed class ConfigCommand : ICommand
{
    public string Name => "config";
    public string Description => "Guild-spezifische Konfiguration";
    public bool AllowAdmin => true;

    private static readonly (string Name, string Value)[] ChannelChoices =
    {
        ("Welcome Channel", "welcome"),
        ("Main Channel", "main"),
        ("Counter Channel", "counterchannel"),
        ("Logs Channel", "logs"),
        ("Bot Channel", "bot"),
        ("Gallery Forum", "gallery_forum"),
        ("Art Channel 1", "art_1"),
        ("Art Channel 2", "art_2"),
        ("Art Channel 3", "art_3"),
        ("Ticket Channel", "ticket"),
        ("Ticket Log Channel", "ticket_log"),
    };

    private static readonly (string Name, string Value)[] RoleChoices =
    {
        ("standard role", "ROLE_UNSCHULDIGES_KIND"),
        ("level 20 role", "ROLE_VERDAECHTIGES_KIND"),
        ("level 40 role", "ROLE_SCHULDIGES_KIND"),
        ("level 60 role", "ROLE_MIT_ENTFUEHRER"),
        ("level 80 role", "ROLE_MEISTERENTFUEHRER"),
        ("level 100 role", "ROLE_BEIFAHRER"),
        ("Booster_role", "ROLE_VAN_UPGRADER"),
        ("Join Role", "join_role"),
        ("Team Role", "team_role"),
        ("Artist Role", "artist_role"),
    };

    private static readonly (string Name, string Value)[] LevelChoices =
    {
        ("Level 20", "20"), ("Level 40", "40"), ("Level 60", "60"),
        ("Level 80", "80"), ("Level 100", "100"),
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
        var whichOption = AddChoices(new SlashCommandOptionBuilder()
            .WithName("which")
            .WithDescription("Which channel config to set")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true), ChannelChoices);

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

        var roleOption = AddChoices(new SlashCommandOptionBuilder()
            .WithName("role")
            .WithDescription("Which configurable role to set")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true), RoleChoices);

        var setRole = new SlashCommandOptionBuilder()
            .WithName("setrole")
            .WithDescription("Set a configured role for this guild")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(roleOption)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("target")
                .WithDescription("The Discord role to assign for this key")
                .WithType(ApplicationCommandOptionType.Role)
                .WithRequired(true));

        var levelOption = AddChoices(new SlashCommandOptionBuilder()
            .WithName("level")
            .WithDescription("Level-Meilenstein")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true), LevelChoices);

        var setLevelRole = new SlashCommandOptionBuilder()
            .WithName("setlevelrole")
            .WithDescription("Rolle festlegen, die bei einem Level-Meilenstein vergeben wird")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(levelOption)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("role")
                .WithDescription("Rolle, die ab diesem Level vergeben wird")
                .WithType(ApplicationCommandOptionType.Role)
                .WithRequired(true));

        return new List<SlashCommandOptionBuilder> { setChannel, setRole, setLevelRole };
    }

    private static SlashCommandOptionBuilder AddChoices(
        SlashCommandOptionBuilder builder, (string Name, string Value)[] choices)
    {
        foreach (var (name, value) in choices)
            builder.AddChoice(name, value);
        return builder;
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
            case "setchannel":
            {
                var which = (string)sub.Options.First(o => o.Name == "which").Value;
                var channel = (IChannel)sub.Options.First(o => o.Name == "channel").Value;
                await _guildConfig.SetAsync(guildId, $"channel:{which}", channel.Id.ToString());
                _logger.LogInformation("config: channel:{Which} = {Channel} ({Guild})", which, channel.Id, guildId);
                await cmd.RespondAsync($"Konfiguration gespeichert: `{which}` → <#{channel.Id}>", ephemeral: true);
                break;
            }
            case "setrole":
            {
                var key = (string)sub.Options.First(o => o.Name == "role").Value;
                var role = (IRole)sub.Options.First(o => o.Name == "target").Value;
                await _guildConfig.SetRoleAsync(guildId, key, role.Id);
                _logger.LogInformation("config: role:{Key} = {Role} ({Guild})", key, role.Id, guildId);
                await cmd.RespondAsync($"Rolle gespeichert: `{key}` → {role.Mention}", ephemeral: true);
                break;
            }
            case "setlevelrole":
            {
                var level = int.Parse((string)sub.Options.First(o => o.Name == "level").Value);
                var role = (IRole)sub.Options.First(o => o.Name == "role").Value;
                await _guildConfig.SetLevelRoleAsync(guildId, level, role.Id);
                _logger.LogInformation("config: levelrole:{Level} = {Role} ({Guild})", level, role.Id, guildId);
                await cmd.RespondAsync($"Level-Rolle gespeichert: Level **{level}** → {role.Mention}", ephemeral: true);
                break;
            }
            default:
                await cmd.RespondAsync("Unbekannter Unterbefehl.", ephemeral: true);
                break;
        }
    }
}
