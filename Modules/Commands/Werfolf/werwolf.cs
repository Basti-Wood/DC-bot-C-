using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Werwolf;

/// <summary>
/// /werwolf_start — erstellt eine Werwolf-Lobby für alle im gleichen
/// Sprachkanal wie der Auslöser. Der Host konfiguriert Werwolf-Anzahl,
/// Bonusrollen und die Team-Zuordnung neutraler Rollen über sein Panel.
/// </summary>
public sealed class WerwolfStartCommand : ICommand
{
    public string Name => "werwolf_start";
    public string Description => "Startet ein Werwolf-Spiel im aktuellen Sprachkanal.";
    public bool AllowEveryone => true;

    private readonly WerwolfGameManager _games;
    private readonly ILoggerFactory _loggerFactory;

    public WerwolfStartCommand(WerwolfGameManager games, ILoggerFactory loggerFactory)
    {
        _games = games;
        _loggerFactory = loggerFactory;
    }

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        await cmd.DeferAsync();

        if (cmd.GuildId is not { } guildId)
        {
            await cmd.FollowupAsync("Dieser Befehl funktioniert nur auf einem Server.", ephemeral: true);
            return;
        }

        if (cmd.User is not SocketGuildUser user)
        {
            await cmd.FollowupAsync("Ich konnte dich nicht als Servermitglied finden.", ephemeral: true);
            return;
        }

        if (user.VoiceChannel is not SocketVoiceChannel voice)
        {
            await cmd.FollowupAsync("Du musst in einem Sprachkanal sein, um ein Spiel zu starten.", ephemeral: true);
            return;
        }

        if (_games.GetByGuild(guildId).Any(g => g.Voice.Id == voice.Id))
        {
            await cmd.FollowupAsync("In diesem Sprachkanal läuft bereits ein Werwolf-Spiel.", ephemeral: true);
            return;
        }

        if (cmd.Channel is not SocketTextChannel channel)
        {
            await cmd.FollowupAsync("Dieser Befehl funktioniert nur in einem Textkanal.", ephemeral: true);
            return;
        }

        var bot = user.Guild.CurrentUser;
        if (bot is null || !bot.GuildPermissions.ManageChannels)
        {
            await cmd.FollowupAsync("Ich brauche die Berechtigung „Kanäle verwalten“, um den Werwolf-Chat zu erstellen.", ephemeral: true);
            return;
        }

        var game = new WerwolfGame(
            _games, user.Guild, channel, user, voice,
            _loggerFactory.CreateLogger<WerwolfGame>());

        if (!_games.TryRegister(game))
        {
            await cmd.FollowupAsync("Das Spiel konnte nicht gestartet werden.", ephemeral: true);
            return;
        }

        await game.BeginLobbyAsync(cmd);
    }
}



