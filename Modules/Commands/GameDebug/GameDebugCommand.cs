using Discord;
using Discord.WebSocket;
using DCBot.Commands.Imposter;
using DCBot.Commands.Werwolf;
using DCBot.Core;

namespace DCBot.Commands.GameDebug;

/// <summary>
/// /game_debug — Owner-only Debug-Einsicht in laufende Werwolf- und
/// Imposter-Spiele (Rollen, Teams, Nachtaktionen, Stimmen, Imposter-Identität,
/// geheime Wörter/Fragen). Antwortet immer ephemeral, damit sonst niemand
/// etwas sieht. Erlaubt für den Bot-Owner (OWNER_ID in der .env) bzw.
/// den Server-Owner.
/// </summary>
public sealed class GameDebugCommand : ICommand
{
    public string Name => "game_debug";
    public string Description => "🔧 Debug: interner Zustand laufender Spiele (nur Owner, ephemeral).";
    public bool AllowAdmin => true;

    private readonly WerwolfGameManager _werwolf;
    private readonly ImposterGameManager _imposter;
    private readonly BotConfig _config;

    public GameDebugCommand(WerwolfGameManager werwolf, ImposterGameManager imposter, BotConfig config)
    {
        _werwolf = werwolf;
        _imposter = imposter;
        _config = config;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("id")
            .WithDescription("Nur ein bestimmtes Spiel anzeigen (GameId oder Anfang davon).")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(false),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        // Owner-Gate: BotConfig.OwnerId (OWNER_ID env) oder Server-Owner
        var isOwner = (_config.OwnerId is { } ownerId && cmd.User.Id == ownerId)
                      || (cmd.User is SocketGuildUser gu && gu.Guild.OwnerId == cmd.User.Id);
        if (!isOwner)
        {
            await cmd.RespondAsync("Nur der Bot-/Server-Owner darf diesen Befehl nutzen.", ephemeral: true);
            return;
        }

        if (cmd.GuildId is not { } guildId)
        {
            await cmd.RespondAsync("Dieser Befehl funktioniert nur auf einem Server.", ephemeral: true);
            return;
        }

        var filter = cmd.Data.Options.FirstOrDefault(o => o.Name == "id")?.Value as string;

        var embeds = _werwolf.GetByGuild(guildId)
            .Where(g => Matches(g.GameId, filter))
            .Select(g => g.BuildDebugEmbed())
            .Concat(_imposter.GetByGuild(guildId)
                .Where(g => Matches(g.GameId, filter))
                .Select(g => g.BuildDebugEmbed()))
            .ToList();

        if (embeds.Count == 0)
        {
            await cmd.RespondAsync(filter is null
                    ? "Auf diesem Server laufen gerade keine Werwolf-/Imposter-Spiele."
                    : $"Kein aktives Spiel mit Id `{filter}` gefunden.",
                ephemeral: true);
            return;
        }

        // Discord erlaubt max. 10 Embeds pro Nachricht
        await cmd.RespondAsync(embeds: embeds.Take(10).ToArray(), ephemeral: true);
    }

    private static bool Matches(string gameId, string? filter)
        => filter is null || gameId.StartsWith(filter, StringComparison.OrdinalIgnoreCase);
}
