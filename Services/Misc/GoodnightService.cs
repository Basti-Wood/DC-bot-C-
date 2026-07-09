using DCBot.Core;
using Discord.WebSocket;

namespace DCBot.Services.Misc;

public sealed class GoodnightService
{
    private const string EnabledKey = "goodnight:enabled";

    private readonly DiscordSocketClient _client;
    private readonly GuildConfigService _guildConfig;
    private readonly TextAssetService _textAssets;

    public GoodnightService(
        DiscordSocketClient client,
        GuildConfigService guildConfig,
        TextAssetService textAssets)
    {
        _client = client;
        _guildConfig = guildConfig;
        _textAssets = textAssets;
    }

    public async Task<bool> IsEnabledAsync(ulong guildId)
    {
        var value = await _guildConfig.GetAsync(guildId, EnabledKey);
        return bool.TryParse(value, out var enabled) && enabled;
    }

    public Task SetEnabledAsync(ulong guildId, bool enabled)
        => _guildConfig.SetAsync(guildId, EnabledKey, enabled.ToString().ToLowerInvariant());

    public async Task<bool> ToggleAsync(ulong guildId)
    {
        var enabled = !await IsEnabledAsync(guildId);
        await SetEnabledAsync(guildId, enabled);
        return enabled;
    }

    public Task<ulong?> GetTargetChannelIdAsync(ulong guildId)
        => _guildConfig.GetChannelAsync(guildId, "main");

    public async Task<bool> SendNowAsync(ulong guildId)
    {
        if (!await IsEnabledAsync(guildId))
            return false;

        var channelId = await GetTargetChannelIdAsync(guildId);
        if (channelId is null)
            return false;

        if (_client.GetGuild(guildId)?.GetTextChannel(channelId.Value) is not { } channel)
            return false;

        var messages = _textAssets.GetGoodnight().Messages;
        if (messages.Count == 0)
            return false;

        await channel.SendMessageAsync(messages[Random.Shared.Next(messages.Count)]);
        return true;
    }

    public async Task<int> SendScheduledMessagesAsync()
    {
        var sentCount = 0;
        var targets = await _guildConfig.GetGuildsWithChannelAsync("main");

        foreach (var (guildId, _) in targets)
        {
            if (await SendNowAsync(guildId))
                sentCount++;
        }

        return sentCount;
    }
}