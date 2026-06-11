using DCBot.Services.Roleplay;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.SetGif;

/// <summary>/setgif reaction url — speichert ein GIF in der Basti API (Admin).</summary>
public sealed class SetGifCommand : ICommand
{
    public string Name => "setgif";
    public string Description => "Speichert eine GIF-URL für eine Reaktion in der Basti API";
    public bool AllowAdmin => true;

    private readonly RoleplayApi _api;
    private readonly ILogger<SetGifCommand> _logger;

    public SetGifCommand(RoleplayApi api, ILogger<SetGifCommand> logger)
    {
        _api = api;
        _logger = logger;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("reaction")
            .WithDescription("Reaktion (z.B. hug, pat, slap)")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true),
        new SlashCommandOptionBuilder()
            .WithName("url")
            .WithDescription("GIF-URL")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        if (!RoleplayApi.IsBastiAvailable())
        {
            await cmd.RespondAsync("Basti API ist nicht verfügbar (BASTIAPI nicht gesetzt).", ephemeral: true);
            return;
        }

        var reaction = (string)cmd.Data.Options.First(o => o.Name == "reaction").Value;
        var url = (string)cmd.Data.Options.First(o => o.Name == "url").Value;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await cmd.RespondAsync("Das ist keine gültige URL.", ephemeral: true);
            return;
        }

        await cmd.DeferAsync(ephemeral: true);

        try
        {
            await _api.SetGifUrlAsync(reaction, url);
            await cmd.FollowupAsync($"GIF für `{reaction}` gespeichert. ✅", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "setgif failed for reaction={Reaction}", reaction);
            await cmd.FollowupAsync($"Fehler beim Speichern: {ex.Message}", ephemeral: true);
        }
    }
}
