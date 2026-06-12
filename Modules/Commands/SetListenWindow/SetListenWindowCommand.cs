using DCBot.Core;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.SetListenWindow;

/// <summary>
/// /setlistenwindow seconds - sets the HeyLookListen burst window for the current guild.
/// </summary>
public sealed class SetListenWindowCommand : ICommand
{
    public string Name => "setlistenwindow";
    public string Description => "Setzt das Zeitfenster fuer HeyLookListen (Sekunden)";
    public bool AllowAdmin => true;

    private const string WindowConfigKey = "heylooklisten:window_seconds";
    private const int MinWindowSeconds = 0;
    private const int MaxWindowSeconds = 120;

    private readonly GuildConfigService _guildConfig;
    private readonly ILogger<SetListenWindowCommand> _logger;

    public SetListenWindowCommand(
        GuildConfigService guildConfig,
        ILogger<SetListenWindowCommand> logger)
    {
        _guildConfig = guildConfig;
        _logger = logger;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("seconds")
            .WithDescription("Zeitfenster in Sekunden (0-120) 0 = deaktiviert")
            .WithType(ApplicationCommandOptionType.Integer)
            .WithRequired(true)
            .WithMinValue(MinWindowSeconds)
            .WithMaxValue(MaxWindowSeconds),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        if (cmd.GuildId is not { } guildId)
        {
            await cmd.RespondAsync("Dieser Befehl funktioniert nur auf einem Server.", ephemeral: true);
            return;
        }

        var option = cmd.Data.Options.FirstOrDefault(o => o.Name == "seconds");
        if (option?.Value is null)
        {
            await cmd.RespondAsync("Option 'seconds' fehlt.", ephemeral: true);
            return;
        }

        var seconds = (int)(long)option.Value;
        seconds = Math.Clamp(seconds, MinWindowSeconds, MaxWindowSeconds);

        await _guildConfig.SetAsync(guildId, WindowConfigKey, seconds.ToString());

        _logger.LogInformation("setlistenwindow: {Seconds}s for guild {Guild}", seconds, guildId);

        await cmd.RespondAsync(
            $"HeyLookListen-Zeitfenster wurde auf **{seconds} Sekunden** gesetzt.",
            ephemeral: true);
    }
}
