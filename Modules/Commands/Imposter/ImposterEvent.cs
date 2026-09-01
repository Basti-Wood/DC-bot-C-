using DCBot.Events;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Imposter;

/// <summary>
/// Routet alle Imposter-Buttons (CustomId-Prefix "imp:") an das passende Spiel.
/// Wird per Reflection als Event-Listener geladen.
/// </summary>
public sealed class ImposterEvent : IEventListener
{
    public string Name => "Imposter";

    private readonly ImposterGameManager _manager;
    private readonly ILogger<ImposterEvent> _logger;

    public ImposterEvent(ImposterGameManager manager, ILogger<ImposterEvent> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public void Register(EventManager events)
    {
        events.OnButtonExecuted(HandleButtonAsync);
        events.OnModalSubmitted(HandleModalAsync);
    }

    private async Task HandleButtonAsync(SocketMessageComponent comp)
    {
        if (!comp.Data.CustomId.StartsWith("imp:", StringComparison.Ordinal)) return;
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
            _logger.LogError(ex, "Imposter button handler failed ({Id})", comp.Data.CustomId);
            await TryErrorAsync(comp);
        }
    }

    /// <summary>CustomId-Format: imp:{gameId}:{action}[:extra]</summary>
    private (ImposterGame? Game, string Action, string? Extra) Resolve(SocketMessageComponent comp)
    {
        var parts = comp.Data.CustomId.Split(':');
        if (parts.Length < 3) return (null, string.Empty, null);
        return (_manager.GetByGameId(parts[1]), parts[2], parts.Length > 3 ? parts[3] : null);
    }

    /// <summary>CustomId-Format für Modals: imp:{gameId}:modal</summary>
    private ImposterGame? ResolveModal(SocketModal modal)
    {
        var parts = modal.Data.CustomId.Split(':');
        if (parts.Length < 3) return null;
        return _manager.GetByGameId(parts[1]);
    }

    private async Task HandleModalAsync(SocketModal modal)
    {
        if (!modal.Data.CustomId.StartsWith("imp:", StringComparison.Ordinal)) return;
        try
        {
            var game = ResolveModal(modal);
            if (game is null)
            {
                await modal.RespondAsync("Dieses Spiel existiert nicht mehr.", ephemeral: true);
                return;
            }
            await game.HandleModalAsync(modal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Imposter modal handler failed ({Id})", modal.Data.CustomId);
            try { await modal.RespondAsync("Da ist etwas schiefgelaufen.", ephemeral: true); }
            catch { /* Interaktion evtl. abgelaufen */ }
        }
    }

    private static async Task TryErrorAsync(SocketMessageComponent comp)
    {
        try { await comp.FollowupAsync("Da ist etwas schiefgelaufen.", ephemeral: true); }
        catch { /* Interaktion evtl. abgelaufen */ }
    }
}
