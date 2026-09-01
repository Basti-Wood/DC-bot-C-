using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Imposter;

/// <summary>
/// /imposter_start — erstellt eine Imposter-Lobby für alle im gleichen
/// Sprachkanal wie der Auslöser. Optionaler Modus-Parameter: "word" oder "question".
/// </summary>
public sealed class ImposterStartCommand : ICommand
{
    public string Name => "imposter_start";
    public string Description => "Startet ein Imposter-Spiel im aktuellen Sprachkanal.";
    public bool AllowEveryone => true;

    private readonly ImposterGameManager _games;
    private readonly ILoggerFactory _loggerFactory;

    public ImposterStartCommand(ImposterGameManager games, ILoggerFactory loggerFactory)
    {
        _games = games;
        _loggerFactory = loggerFactory;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() =>
    [
        new SlashCommandOptionBuilder()
            .WithName("modus")
            .WithDescription("Spielmodus: word (Standard) oder question")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(false)
            .AddChoice("Wort", "word")
            .AddChoice("Frage", "question"),
    ];

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
            await cmd.FollowupAsync("In diesem Sprachkanal läuft bereits ein Imposter-Spiel.", ephemeral: true);
            return;
        }

        if (cmd.Channel is not SocketTextChannel channel)
        {
            await cmd.FollowupAsync("Dieser Befehl funktioniert nur in einem Textkanal.", ephemeral: true);
            return;
        }

        // Modus aus Option lesen
        var modeOption = cmd.Data.Options.FirstOrDefault(o => o.Name == "modus");
        var mode = ImposterGame.ImposterMode.Word;
        if (modeOption?.Value is string modeStr && modeStr.Equals("question", StringComparison.OrdinalIgnoreCase))
            mode = ImposterGame.ImposterMode.Question;

        var game = new ImposterGame(
            _games, user.Guild, channel, user, voice, mode,
            _loggerFactory.CreateLogger<ImposterGame>());

        if (!_games.TryRegister(game))
        {
            await cmd.FollowupAsync("Das Spiel konnte nicht gestartet werden.", ephemeral: true);
            return;
        }

        await game.BeginLobbyAsync(cmd);
    }
}

// /imposter_answer wurde durch den "📝 Antwort abgeben" Button + Modal ersetzt.
// Die Modal-Logik lebt in ImposterGame.HandleModalAsync und wird vom ImposterEvent geroutet.
