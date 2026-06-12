using DCBot.Core;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.JoinRole;

/// <summary>Vergibt die konfigurierte join_role beim Beitritt.</summary>
public sealed class JoinRoleEvent : IEventListener
{
    public string Name => "JoinRole";

    private readonly GuildConfigService _guildConfig;
    private readonly ILogger<JoinRoleEvent> _logger;

    public JoinRoleEvent(GuildConfigService guildConfig, ILogger<JoinRoleEvent> logger)
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
        var roleId = await _guildConfig.GetRoleAsync(user.Guild.Id, "join_role");
        if (roleId is null) return;
        if (user.Roles.Any(r => r.Id == roleId)) return;

        try
        {
            await user.AddRoleAsync(roleId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "joinrole: failed to assign role to {User} in {Guild}",
                user.Id, user.Guild.Id);
        }
    }
}
