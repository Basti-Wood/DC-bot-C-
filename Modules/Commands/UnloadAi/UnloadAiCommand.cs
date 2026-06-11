using DCBot.Services.Ai;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.UnloadAi;

/// <summary>/unloadai — entlädt das Modell der Basti API (Admin).</summary>
public sealed class UnloadAiCommand : ICommand
{
    public string Name => "unloadai";
    public string Description => "Entlädt das Basti-AI-Modell";
    public bool AllowAdmin => true;

    private readonly AiService _ai;
    private readonly ILogger<UnloadAiCommand> _logger;

    public UnloadAiCommand(AiService ai, ILogger<UnloadAiCommand> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        if (!AiService.IsBastiAvailable())
        {
            await cmd.RespondAsync("BASTIAPI ist nicht gesetzt — Basti nicht verfügbar.", ephemeral: true);
            return;
        }

        await cmd.DeferAsync(ephemeral: true);

        try
        {
            await _ai.UnloadBastiAsync();
            await cmd.FollowupAsync("Basti-Modell entladen. ✅", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unloadai failed");
            await cmd.FollowupAsync($"Fehler beim Entladen: {ex.Message}", ephemeral: true);
        }
    }
}
