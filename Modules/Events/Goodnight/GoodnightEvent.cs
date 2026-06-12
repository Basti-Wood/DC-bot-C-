using DCBot.Core;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.Goodnight;

/// <summary>
/// Schickt jede Mitternacht (Europe/Berlin) eine Gute-Nacht-Nachricht in
/// alle konfigurierten Main-Channels (Port von goodnight_message.go).
/// </summary>
public sealed class GoodnightEvent : IEventListener
{
    public string Name => "Goodnight";

    private readonly DiscordSocketClient _client;
    private readonly GuildConfigService _guildConfig;
    private readonly ILogger<GoodnightEvent> _logger;
    private int _started;

    public GoodnightEvent(
        DiscordSocketClient client,
        GuildConfigService guildConfig,
        ILogger<GoodnightEvent> logger)
    {
        _client = client;
        _guildConfig = guildConfig;
        _logger = logger;
    }

    public void Register(EventManager events)
    {
        events.OnReady(() =>
        {
            if (Interlocked.Exchange(ref _started, 1) == 0)
                _ = Task.Run(LoopAsync);
            return Task.CompletedTask;
        });
    }

    private async Task LoopAsync()
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); // Windows-Fallback
        }

        while (true)
        {
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            var nextMidnight = new DateTimeOffset(
                now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).AddDays(1);
            var wait = nextMidnight - now;

            _logger.LogInformation("goodnight: next message in {Wait}", wait);
            await Task.Delay(wait);

            await SendGoodnightMessagesAsync();
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private async Task SendGoodnightMessagesAsync()
    {
        var targets = await _guildConfig.GetGuildsWithChannelAsync("main");

        foreach (var (guildId, channelId) in targets)
        {
            try
            {
                if (_client.GetGuild(guildId)?.GetTextChannel(channelId) is { } channel)
                    await channel.SendMessageAsync(
                        "Gute Nacht, Gefangene des Vans! <:TuubaaAwake:1244353418894643271>");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "goodnight: failed to send to {Channel}", channelId);
            }
        }
    }
}
