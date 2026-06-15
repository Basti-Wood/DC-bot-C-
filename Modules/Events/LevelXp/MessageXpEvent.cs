using System.Collections.Concurrent;
using DCBot.Core;
using DCBot.Services.Level;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.LevelXp;

/// <summary>
/// Vergibt XP für Chat-Nachrichten (Port von message_event.go, vereinfacht):
/// 1 XP-Gewinn pro User pro 25 s, zufällig 15–25 XP, Stunden-/Tageslimit.
/// Level-Ups werden im konfigurierten Bot-Channel angekündigt.
/// </summary>
public sealed class MessageXpEvent : IEventListener
{
    public string Name => "LevelXp";

    private const int CooldownSeconds = 25;
    private const long HourlyXpLimit = 800;
    private const long DailyXpLimit = 5000;

    private readonly LevelRepository _repo;
    private readonly GuildConfigService _guildConfig;
    private readonly ILogger<MessageXpEvent> _logger;
    private readonly Random _random = new();

    private readonly ConcurrentDictionary<ulong, DateTimeOffset> _lastGain = new();
    private readonly ConcurrentDictionary<ulong, (DateTimeOffset Window, long Earned)> _hourly = new();
    private readonly ConcurrentDictionary<ulong, (DateTimeOffset Window, long Earned)> _daily = new();

    public MessageXpEvent(
        LevelRepository repo,
        GuildConfigService guildConfig,
        ILogger<MessageXpEvent> logger)
    {
        _repo = repo;
        _guildConfig = guildConfig;
        _logger = logger;
    }

    public void Register(EventManager events)
    {
        events.OnMessageReceived(HandleAsync);
    }

    private async Task HandleAsync(SocketMessage message)
    {
        if (message.Author.IsBot) return;
        if (message.Author is not SocketGuildUser user) return;
        if (message.Channel is not SocketGuildChannel channel) return;

        var now = DateTimeOffset.UtcNow;

        if (_lastGain.TryGetValue(user.Id, out var last)
            && (now - last).TotalSeconds < CooldownSeconds)
            return;
        _lastGain[user.Id] = now;

        long amount = _random.Next(15, 26);
        amount = ClampToWindow(_hourly, user.Id, amount, now, TimeSpan.FromHours(1), HourlyXpLimit);
        if (amount <= 0) return;
        amount = ClampToWindow(_daily, user.Id, amount, now, TimeSpan.FromDays(1), DailyXpLimit);
        if (amount <= 0) return;

        var guildId = channel.Guild.Id;
        var before = await _repo.GetXpAsync(guildId, user.Id);
        var after = await _repo.AddXpAsync(guildId, user.Id, amount);

        var prevLevel = XpMath.CalcLevel(before);
        var newLevel = XpMath.CalcLevel(after);

        if (newLevel > prevLevel)
            await AnnounceLevelUpAsync(channel.Guild, user, newLevel, message.Channel);
    }

    private static long ClampToWindow(
        ConcurrentDictionary<ulong, (DateTimeOffset Window, long Earned)> store,
        ulong userId, long amount, DateTimeOffset now, TimeSpan windowSize, long limit)
    {
        var window = new DateTimeOffset(now.Ticks - now.Ticks % windowSize.Ticks, TimeSpan.Zero);
        var (currentWindow, earned) = store.GetOrAdd(userId, (window, 0L));

        if (currentWindow != window)
            earned = 0;

        if (earned >= limit) return 0;
        var allowed = Math.Min(amount, limit - earned);
        store[userId] = (window, earned + allowed);
        return allowed;
    }

    private async Task AnnounceLevelUpAsync(
        SocketGuild guild, SocketGuildUser user, int newLevel, ISocketMessageChannel fallback)
    {
        try
        {
            IMessageChannel target = fallback;
            var configured = await _guildConfig.GetChannelAsync(guild.Id, "bot");
            if (configured is { } channelId && guild.GetTextChannel(channelId) is { } botChannel)
                target = botChannel;

            var embed = new EmbedBuilder()
                .WithTitle("Level Up! 🎉")
                .WithDescription($"{user.Mention} ist jetzt **Level {newLevel}**!")
                .WithColor(Color.Gold)
                .WithThumbnailUrl(user.GetDisplayAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .Build();

            await target.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to announce level up for {User}", user.Id);
        }
    }
}
