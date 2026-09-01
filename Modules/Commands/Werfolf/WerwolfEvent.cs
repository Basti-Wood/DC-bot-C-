using DCBot.Events;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Werwolf;

/// <summary>
/// Routet alle Werwolf-Buttons und -Select-Menüs (CustomId-Prefix "ww:")
/// an das passende Spiel. Wird per Reflection als Event-Listener geladen.
/// </summary>
public sealed class WerwolfEvent : IEventListener
{
    public string Name => "Werwolf";

    private readonly WerwolfGameManager _manager;
    private readonly ILogger<WerwolfEvent> _logger;

    public WerwolfEvent(WerwolfGameManager manager, ILogger<WerwolfEvent> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public void Register(EventManager events)
    {
        events.OnButtonExecuted(HandleButtonAsync);
        events.OnSelectMenuExecuted(HandleSelectAsync);
    }

    private async Task HandleButtonAsync(SocketMessageComponent comp)
    {
        if (!comp.Data.CustomId.StartsWith("ww:", StringComparison.Ordinal)) return;
        await comp.DeferAsync();
        try
        {
            var (game, action, extra) = Resolve(comp);
            if (game is null)
            {
                await comp.FollowupAsync("Dieses Spiel existiert nicht mehr.", ephemeral: true);
                return;
            }
            await game.HandleButtonAsync(comp, action, extra);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Werwolf button handler failed ({Id})", comp.Data.CustomId);
            await TryErrorAsync(comp);
        }
    }

    private async Task HandleSelectAsync(SocketMessageComponent comp)
    {
        if (!comp.Data.CustomId.StartsWith("ww:", StringComparison.Ordinal)) return;
        await comp.DeferAsync();
        try
        {
            var (game, action, extra) = Resolve(comp);
            if (game is null)
            {
                await comp.FollowupAsync("Dieses Spiel existiert nicht mehr.", ephemeral: true);
                return;
            }
            await game.HandleSelectAsync(comp, action, extra);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Werwolf select handler failed ({Id})", comp.Data.CustomId);
            await TryErrorAsync(comp);
        }
    }

    /// <summary>CustomId-Format: ww:{gameId}:{action}[:extra]</summary>
    private (WerwolfGame? Game, string Action, string? Extra) Resolve(SocketMessageComponent comp)
    {
        var parts = comp.Data.CustomId.Split(':');
        if (parts.Length < 3) return (null, string.Empty, null);
        return (_manager.GetByGameId(parts[1]), parts[2], parts.Length > 3 ? parts[3] : null);
    }

    private static async Task TryErrorAsync(SocketMessageComponent comp)
    {
        try { await comp.FollowupAsync("Da ist etwas schiefgelaufen.", ephemeral: true); }
        catch { /* Interaktion evtl. abgelaufen */ }
    }
}
