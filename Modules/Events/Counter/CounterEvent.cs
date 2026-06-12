using System.Collections.Concurrent;
using DCBot.Core;
using DCBot.Services.Flavor;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.Counter;

/// <summary>
/// Aktualisiert den Member-Counter-Channel bei Join/Leave —
/// max. 1× alle 5 Minuten pro Guild (Port von counter_events.go).
/// </summary>
public sealed class CounterEvent : IEventListener
{
    public string Name => "Counter";

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMinutes(5);

    private readonly GuildConfigService _guildConfig;
    private readonly FlavorService _flavor;
    private readonly ILogger<CounterEvent> _logger;
    private readonly ConcurrentDictionary<ulong, DateTimeOffset> _lastUpdate = new();

    public CounterEvent(GuildConfigService guildConfig, FlavorService flavor, ILogger<CounterEvent> logger)
    {
        _guildConfig = guildConfig;
        _flavor = flavor;
        _logger = logger;
    }

    public void Register(EventManager events)
    {
        events.OnUserJoined(user => UpdateAsync(user.Guild));
        events.OnUserLeft((guild, _) => UpdateAsync(guild));
    }

    private async Task UpdateAsync(SocketGuild guild)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastUpdate.TryGetValue(guild.Id, out var last) && now - last < UpdateInterval)
        {
            _logger.LogDebug("counter: skipping update for {Guild} (rate limited)", guild.Id);
            return;
        }
        _lastUpdate[guild.Id] = now;

        var channelId = await _guildConfig.GetChannelAsync(guild.Id, "counterchannel");
        if (channelId is null || guild.GetChannel(channelId.Value) is not { } channel)
            return;

        try
        {
            var template = await _flavor.GetCounterTemplateAsync(guild.Id);
            var newName = FlavorService.RenderCounterName(template, guild.MemberCount);
            await channel.ModifyAsync(p => p.Name = newName);
            _logger.LogDebug("counter: updated {Guild} -> {Name}", guild.Id, newName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "counter: failed to update channel {Channel}", channelId);
        }
    }
}
