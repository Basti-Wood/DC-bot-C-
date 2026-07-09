using DCBot.Core;
using DCBot.Services.Misc;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.Goodnight;

/// <summary>
/// Schickt jede Mitternacht (Europe/Berlin) eine Gute-Nacht-Nachricht in
/// alle aktivierten Main-Channels.
/// </summary>
public sealed class GoodnightEvent : IEventListener
{
    public string Name => "Goodnight";

    private readonly GoodnightService _goodnight;
    private readonly ILogger<GoodnightEvent> _logger;
    private int _started;

    public GoodnightEvent(
        GoodnightService goodnight,
        ILogger<GoodnightEvent> logger)
    {
        _goodnight = goodnight;
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

            var sentCount = await _goodnight.SendScheduledMessagesAsync();
            _logger.LogInformation("goodnight: sent scheduled messages to {Count} guild(s)", sentCount);
        }
        // ReSharper disable once FunctionNeverReturns
    }
}
