using DCBot.Core;
using DCBot.Services.Misc;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.Welcome;

/// <summary>
/// Willkommensnachricht (Port von welcome_message.go):
/// - Accounts jünger als 15 Tage: DM + Kick-Hinweis, keine Begrüßung
/// - 5 s Wartezeit, dann Check ob der User noch im Server ist
/// - Embed in den Welcome-Channel (statt generiertem PNG im Go-Bot)
/// - Ankündigung im Main-Channel
/// </summary>
public sealed class WelcomeEvent : IEventListener
{
    public string Name => "Welcome";

    private static readonly TimeSpan MinAccountAge = TimeSpan.FromDays(15);

    private readonly GuildConfigService _guildConfig;
    private readonly TextAssetService _textAssets;
    private readonly ILogger<WelcomeEvent> _logger;

    public WelcomeEvent(
        GuildConfigService guildConfig,
        TextAssetService textAssets,
        ILogger<WelcomeEvent> logger)
    {
        _guildConfig = guildConfig;
        _textAssets = textAssets;
        _logger = logger;
    }

    public void Register(EventManager events)
    {
        events.OnUserJoined(HandleAsync);
    }

    private async Task HandleAsync(SocketGuildUser user)
    {
        var guild = user.Guild;
        _logger.LogDebug("welcome: join from {User} in {Guild}", user.Id, guild.Id);

        // Account-Alter prüfen (Discord-Snowflake = Erstellungszeit)
        var accountAge = DateTimeOffset.UtcNow - user.CreatedAt;
        if (accountAge < MinAccountAge)
        {
            _logger.LogDebug("welcome: account {User} too new ({Days} days), sending DM",
                user.Id, (int)accountAge.TotalDays);
            var welcomeText = _textAssets.GetWelcome();
            try
            {
                var dm = await user.CreateDMChannelAsync();
                await dm.SendMessageAsync(welcomeText.UnderageDm);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "welcome: failed to DM {User}", user.Id);
            }
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(5));

        // Noch im Server?
        if (guild.GetUser(user.Id) is null)
        {
            _logger.LogDebug("welcome: {User} left before welcome, skipping", user.Id);
            return;
        }

        var welcomeChannelId = await _guildConfig.GetChannelAsync(guild.Id, "welcome");
        var mainChannelId = await _guildConfig.GetChannelAsync(guild.Id, "main");
        var welcomeTextAsset = _textAssets.GetWelcome();
        var templateValues = new Dictionary<string, string>
        {
            ["displayName"] = user.DisplayName ?? user.GlobalName ?? user.Username,
            ["memberCount"] = guild.MemberCount.ToString(),
            ["userMention"] = user.Mention,
            ["mainChannelMention"] = mainChannelId is { } mainId ? $"<#{mainId}>" : string.Empty,
        };

        if (welcomeChannelId is { } wcId && guild.GetTextChannel(wcId) is { } welcomeChannel)
        {
            var descriptionTemplate = mainChannelId is { }
                ? welcomeTextAsset.EmbedDescriptionWithMainChannel
                : welcomeTextAsset.EmbedDescriptionWithoutMainChannel;
            var embed = new EmbedBuilder()
                .WithTitle(TextAssetService.RenderTemplate(welcomeTextAsset.EmbedTitle, templateValues))
                .WithDescription(TextAssetService.RenderTemplate(descriptionTemplate, templateValues))
                .WithThumbnailUrl(user.GetDisplayAvatarUrl(size: 1024) ?? user.GetDefaultAvatarUrl())
                .WithFooter(TextAssetService.RenderTemplate(welcomeTextAsset.EmbedFooter, templateValues))
                .WithColor(new Color(0x9b59b6))
                .WithCurrentTimestamp()
                .Build();

            try
            {
                await welcomeChannel.SendMessageAsync(embed: embed,
                    flags: MessageFlags.SuppressNotification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "welcome: send failed to {Channel}", wcId);
            }
        }

        if (mainChannelId is { } mcId && guild.GetTextChannel(mcId) is { } mainChannel)
        {
            try
            {
                await mainChannel.SendMessageAsync(
                    TextAssetService.RenderTemplate(welcomeTextAsset.MainAnnouncement, templateValues));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "welcome: main announcement failed to {Channel}", mcId);
            }
        }
    }
}
