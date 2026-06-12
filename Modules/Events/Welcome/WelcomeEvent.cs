using DCBot.Core;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.Welcome;

/// <summary>
/// Willkommensnachricht (Port von welcome_message.go):
/// - Accounts jünger als 2 Monate: DM + Kick-Hinweis, keine Begrüßung
/// - 5 s Wartezeit, dann Check ob der User noch im Server ist
/// - Embed in den Welcome-Channel (statt generiertem PNG im Go-Bot)
/// - Ankündigung im Main-Channel
/// </summary>
public sealed class WelcomeEvent : IEventListener
{
    public string Name => "Welcome";

    private static readonly TimeSpan MinAccountAge = TimeSpan.FromDays(60);

    private readonly GuildConfigService _guildConfig;
    private readonly ILogger<WelcomeEvent> _logger;

    public WelcomeEvent(GuildConfigService guildConfig, ILogger<WelcomeEvent> logger)
    {
        _guildConfig = guildConfig;
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
            try
            {
                var dm = await user.CreateDMChannelAsync();
                await dm.SendMessageAsync(
                    "Dein Discord Account ist leider noch nicht alt genug, um dem Server beizutreten. " +
                    "Bitte versuche es erneut, wenn dein Account mindestens 2 Monate alt ist.");
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

        if (welcomeChannelId is { } wcId && guild.GetTextChannel(wcId) is { } welcomeChannel)
        {
            var displayName = user.DisplayName ?? user.GlobalName ?? user.Username;
            var embed = new EmbedBuilder()
                .WithTitle($"Willkommen im goldenen Van, {displayName}! 💜")
                .WithDescription(mainChannelId is { } mc
                    ? $"Schau doch mal in <#{mc}> vorbei!"
                    : "Viel Spaß auf dem Server!")
                .WithThumbnailUrl(user.GetDisplayAvatarUrl(size: 1024) ?? user.GetDefaultAvatarUrl())
                .WithFooter($"Mitglied #{guild.MemberCount}")
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
                    $"Ein neuer gefangener im **goldenen Van**! Heißt <@{user.Id}> willkommen <:HeyTuba:1137369596496707624>");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "welcome: main announcement failed to {Channel}", mcId);
            }
        }
    }
}
