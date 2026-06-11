using DCBot.Services.Ai;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.LoadAi;

/// <summary>/loadai — lädt das Modell der Basti API (Admin).</summary>
public sealed class LoadAiCommand : ICommand
{
    public string Name => "loadai";
    public string Description => "Lädt das Basti-AI-Modell";
    public bool AllowAdmin => true;

    private readonly AiService _ai;
    private readonly ILogger<LoadAiCommand> _logger;

    public LoadAiCommand(AiService ai, ILogger<LoadAiCommand> logger)
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
            await _ai.LoadBastiAsync();
            await cmd.FollowupAsync("Basti-Modell geladen. ✅", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "loadai failed");
            await cmd.FollowupAsync($"Fehler beim Laden: {ex.Message}", ephemeral: true);
        }
    }
}
