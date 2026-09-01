using System.Text;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Imposter;

/// <summary>
/// Ein laufendes Imposter-Spiel. Phasen: Lobby → Spielrunde → Voting → Ende.
/// Unterstützt zwei Modi:
///   - "word":     Alle bekommen das gleiche Wort per DM, der Imposter ein ähnliches anderes.
///   - "question": Alle beantworten die gleiche Frage, der Imposter beantwortet eine andere.
///                 Antworten werden öffentlich gepostet.
/// Der Imposter weiß nicht, dass er der Imposter ist.
/// </summary>
public sealed class ImposterGame
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // ---------------------------------------------------------------- State

    public string GameId { get; } = Guid.NewGuid().ToString("N")[..8];
    public SocketGuild Guild { get; }
    public SocketTextChannel LobbyChannel { get; }
    public SocketGuildUser Host { get; }
    public SocketVoiceChannel Voice { get; }
    public ImposterMode Mode { get; }

    public enum ImposterMode { Word, Question }

    /// <summary>Alle beigetretenen Spieler (UserId → User).</summary>
    public Dictionary<ulong, SocketGuildUser> Players { get; } = new();

    /// <summary>UserId des Imposters (null bis Spielstart).</summary>
    public ulong? ImposterId { get; private set; }

    /// <summary>Das Wort/die Frage für normale Spieler.</summary>
    public string PlayerContent { get; private set; } = "";

    /// <summary>Das Wort/die Frage für den Imposter.</summary>
    public string ImposterContent { get; private set; } = "";

    /// <summary>UserId → abgegebene Stimme (Voting).</summary>
    public Dictionary<ulong, ulong> Votes { get; } = new();

    /// <summary>UserId → eingereichte Antwort (question-Modus).</summary>
    public Dictionary<ulong, string> Answers { get; } = new();

    /// <summary>Ob das Spiel gerade in der Voting-Phase ist.</summary>
    public bool IsVoting { get; private set; }

    /// <summary>Ob das Spiel beendet ist.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Wer als Imposter gevotet wurde (null = niemand korrekt).</summary>
    public ulong? VotedPlayerId { get; private set; }

    private readonly ImposterGameManager _manager;
    private readonly ILogger<ImposterGame> _logger;
    private IUserMessage? _gameMessage;

    public ImposterGame(
        ImposterGameManager manager,
        SocketGuild guild,
        SocketTextChannel lobbyChannel,
        SocketGuildUser host,
        SocketVoiceChannel voice,
        ImposterMode mode,
        ILogger<ImposterGame> logger)
    {
        _manager = manager;
        Guild = guild;
        LobbyChannel = lobbyChannel;
        Host = host;
        Voice = voice;
        Mode = mode;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Helpers

    public string ComponentId(string action, string? extra = null)
        => extra is null ? $"imp:{GameId}:{action}" : $"imp:{GameId}:{action}:{extra}";

    private SocketGuildUser? GetPlayer(ulong userId) => Players.GetValueOrDefault(userId);

    // ---------------------------------------------------------------- Assets laden

    private static ImposterWordsAsset? LoadWordsAsset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "text", "imposter_words.json");
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ImposterWordsAsset>(json, JsonOptions);
    }

    private static ImposterQuestionsAsset? LoadQuestionsAsset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "text", "imposter_questions.json");
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ImposterQuestionsAsset>(json, JsonOptions);
    }

    /// <summary>
    /// Wählt eine zufällige Gruppe und daraus 2 verschiedene Wörter.
    /// Das erste ist für die Spieler, das zweite für den Imposter.
    /// </summary>
    private (string Player, string Imposter) PickWords()
    {
        var asset = LoadWordsAsset();
        var groups = asset?.Groups ?? [];
        if (groups.Count == 0)
            return ("Wort", "Wort");

        var group = groups[Random.Shared.Next(groups.Count)];
        if (group.Words.Count < 2)
            return (group.Words.FirstOrDefault() ?? "Wort", group.Words.LastOrDefault() ?? "Wort");

        var shuffled = group.Words.OrderBy(_ => Random.Shared.Next()).ToList();
        return (shuffled[0], shuffled[1]);
    }

    /// <summary>
    /// Wählt eine zufällige Frage-Gruppe und daraus 2 verschiedene Fragen.
    /// Beide Fragen führen zu ähnlichen Antwortkategorien,
    /// sodass der Imposter nicht sofort auffällt.
    /// </summary>
    private (string Player, string Imposter) PickQuestions()
    {
        var asset = LoadQuestionsAsset();
        var groups = asset?.Groups ?? [];
        if (groups.Count == 0)
            return ("Frage", "Frage");

        var group = groups[Random.Shared.Next(groups.Count)];
        if (group.Questions.Count < 2)
            return (group.Questions.FirstOrDefault() ?? "Frage", group.Questions.LastOrDefault() ?? "Frage");

        var shuffled = group.Questions.OrderBy(_ => Random.Shared.Next()).ToList();
        return (shuffled[0], shuffled[1]);
    }

    // ---------------------------------------------------------------- Lobby

    public async Task BeginLobbyAsync(SocketSlashCommand cmd)
    {
        Players[Host.Id] = Host;

        var embed = new EmbedBuilder()
            .WithTitle("🕵️ Imposter — Lobby")
            .WithDescription(BuildLobbyDescription())
            .WithColor(Color.Blue)
            .WithFooter($"Modus: {(Mode == ImposterMode.Word ? "Wort" : "Frage")}")
            .Build();

        var components = new ComponentBuilder()
            .WithButton("🎭 Beitreten", ComponentId("join"), ButtonStyle.Success)
            .WithButton("▶️ Spiel starten", ComponentId("start"), ButtonStyle.Primary)
            .WithButton("❌ Abbrechen", ComponentId("cancel"), ButtonStyle.Danger)
            .Build();

        _gameMessage = await cmd.FollowupAsync(embed: embed, components: components);
    }

    private string BuildLobbyDescription()
    {
        var lines = new List<string>
        {
            $"**Host:** {Host.Mention}",
            $"**Sprachkanal:** {Voice.Name}",
            "",
            "**Spieler:**",
        };

        if (Players.Count == 0)
            lines.Add("*Noch niemand beigetreten.*");
        else
            lines.AddRange(Players.Values.Select(u => $"• {u.Mention}"));

        return string.Join('\n', lines);
    }

    // ---------------------------------------------------------------- Button-Routing

    public async Task HandleButtonAsync(SocketMessageComponent comp, string action, string? extra)
    {
        if (IsFinished) return;

        switch (action)
        {
            case "join":
                await HandleJoinAsync(comp);
                break;
            case "start":
                await HandleStartAsync(comp);
                break;
            case "cancel":
                await HandleCancelAsync(comp);
                break;
            case "answer":
                await HandleAnswerButtonAsync(comp);
                break;
            case "endround":
                await HandleEndRoundAsync(comp);
                break;
            case "vote":
                await HandleVoteAsync(comp, extra);
                break;
        }
    }

    // ---------------------------------------------------------------- Lobby-Buttons

    private async Task HandleJoinAsync(SocketMessageComponent comp)
    {
        if (comp.User is not SocketGuildUser user) return;

        if (Players.ContainsKey(user.Id))
        {
            await comp.FollowupAsync("Du bist bereits dabei.", ephemeral: true);
            return;
        }

        if (IsVoting || IsFinished)
        {
            await comp.FollowupAsync("Das Spiel läuft bereits.", ephemeral: true);
            return;
        }

        Players[user.Id] = user;

        if (_gameMessage is not null)
        {
            var embed = new EmbedBuilder()
                .WithTitle("🕵️ Imposter — Lobby")
                .WithDescription(BuildLobbyDescription())
                .WithColor(Color.Blue)
                .WithFooter($"Modus: {(Mode == ImposterMode.Word ? "Wort" : "Frage")}")
                .Build();

            await _gameMessage.ModifyAsync(p =>
            {
                p.Embed = embed;
            });
        }

        await comp.FollowupAsync("Du bist dem Spiel beigetreten! 🎉", ephemeral: true);
    }

    private async Task HandleStartAsync(SocketMessageComponent comp)
    {
        if (comp.User.Id != Host.Id)
        {
            await comp.FollowupAsync("Nur der Host kann das Spiel starten.", ephemeral: true);
            return;
        }

        if (Players.Count < 3)
        {
            await comp.FollowupAsync("Mindestens 3 Spieler werden benötigt.", ephemeral: true);
            return;
        }

        // Imposter zufällig wählen
        var playerList = Players.Values.ToList();
        var imposter = playerList[Random.Shared.Next(playerList.Count)];
        ImposterId = imposter.Id;

        // Inhalt wählen je nach Modus
        string playerContent, imposterContent;
        if (Mode == ImposterMode.Question)
            (playerContent, imposterContent) = PickQuestions();
        else
            (playerContent, imposterContent) = PickWords();

        PlayerContent = playerContent;
        ImposterContent = imposterContent;

        // DMs senden
        int dmSuccess = 0, dmFail = 0;
        foreach (var player in Players.Values)
        {
            var isImposter = player.Id == imposter.Id;
            var content = isImposter ? ImposterContent : PlayerContent;

            var title = Mode == ImposterMode.Word
                ? "📝 Dein Wort für diese Runde"
                : "❓ Deine Frage für diese Runde";

            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(content)
                .WithColor(isImposter ? Color.Orange : Color.Green)
                .WithFooter(Mode == ImposterMode.Word
                    ? "Beschreibe dein Wort, ohne es zu nennen."
                    : "Beantworte die Frage über den \"📝 Antwort abgeben\" Button.")
                .Build();

            var ok = await TryDmAsync(player, embed);
            if (ok) dmSuccess++; else dmFail++;
        }

        // Spiel-Nachricht aktualisieren
        if (_gameMessage is not null)
        {
            var gameEmbed = new EmbedBuilder()
                .WithTitle("🕵️ Imposter — Runde läuft!")
                .WithDescription(BuildGameDescription())
                .WithColor(Color.Gold)
                .WithFooter($"Modus: {(Mode == ImposterMode.Word ? "Wort" : "Frage")}")
                .Build();

            var gameComponentsBuilder = new ComponentBuilder()
                .WithButton("🔚 Runde beenden", ComponentId("endround"), ButtonStyle.Danger);

            if (Mode == ImposterMode.Question)
                gameComponentsBuilder.WithButton("📝 Antwort abgeben", ComponentId("answer"), ButtonStyle.Primary);

            var gameComponents = gameComponentsBuilder.Build();

            await _gameMessage.ModifyAsync(p =>
            {
                p.Embed = gameEmbed;
                p.Components = gameComponents;
            });
        }

        var statusMsg = dmFail > 0
            ? $"DMs gesendet: {dmSuccess} ✅ / {dmFail} ❌ (DMs evtl. deaktiviert)"
            : $"Alle {dmSuccess} DMs wurden gesendet!";

        await comp.FollowupAsync($"🕵️ Das Spiel wurde gestartet! {statusMsg}", ephemeral: true);
    }

    private async Task HandleCancelAsync(SocketMessageComponent comp)
    {
        if (comp.User.Id != Host.Id)
        {
            await comp.FollowupAsync("Nur der Host kann das Spiel abbrechen.", ephemeral: true);
            return;
        }

        IsFinished = true;

        var embed = new EmbedBuilder()
            .WithTitle("🕵️ Imposter — Abgebrochen")
            .WithDescription("Das Spiel wurde vom Host abgebrochen.")
            .WithColor(Color.DarkGrey)
            .Build();

        if (_gameMessage is not null)
            await _gameMessage.ModifyAsync(p => { p.Embed = embed; p.Components = new ComponentBuilder().Build(); });

        _manager.Remove(this);
        await comp.FollowupAsync("Spiel abgebrochen.", ephemeral: true);
    }

    // ---------------------------------------------------------------- Runde beenden → Voting

    private async Task HandleEndRoundAsync(SocketMessageComponent comp)
    {
        if (comp.User.Id != Host.Id)
        {
            await comp.FollowupAsync("Nur der Host kann die Runde beenden.", ephemeral: true);
            return;
        }

        if (IsVoting)
        {
            await comp.FollowupAsync("Die Abstimmung läuft bereits.", ephemeral: true);
            return;
        }

        IsVoting = true;
        Votes.Clear();

        // Im question-Modus: Antworten veröffentlichen
        if (Mode == ImposterMode.Question)
        {
            if (Answers.Count > 0)
            {
                var answersEmbed = new EmbedBuilder()
                    .WithTitle("📋 Eingereichte Antworten")
                    .WithDescription($"**Frage (Spieler):** {PlayerContent}")
                    .WithColor(Color.Blue);

                foreach (var (userId, answer) in Answers)
                {
                    if (GetPlayer(userId) is { } player)
                        answersEmbed.AddField(player.DisplayName, answer, inline: true);
                }

                await LobbyChannel.SendMessageAsync(embed: answersEmbed.Build());
            }
            else
            {
                await LobbyChannel.SendMessageAsync("⚠️ Keine Antworten wurden eingereicht.");
            }
        }

        // Voting-Buttons aufbauen
        var cb = new ComponentBuilder();
        foreach (var player in Players.Values)
        {
            var label = player.DisplayName.Length > 20 ? player.DisplayName[..20] : player.DisplayName;
            cb.WithButton($"🗳️ {label}", ComponentId("vote", player.Id.ToString()), ButtonStyle.Secondary);
        }

        var voteEmbed = new EmbedBuilder()
            .WithTitle("🗳️ Wer ist der Imposter?")
            .WithDescription("Stimmt ab! Die Mehrheit entscheidet.")
            .WithColor(Color.Red)
            .WithFooter($"{Players.Count} Stimmen benötigt für Mehrheit")
            .Build();

        if (_gameMessage is not null)
        {
            await _gameMessage.ModifyAsync(p =>
            {
                p.Embed = voteEmbed;
                p.Components = cb.Build();
            });
        }

        await comp.FollowupAsync("Die Abstimmung hat begonnen! 🗳️", ephemeral: true);
    }

    // ---------------------------------------------------------------- Voting

    private async Task HandleVoteAsync(SocketMessageComponent comp, string? extra)
    {
        if (comp.User is not SocketGuildUser voter) return;

        if (!Players.ContainsKey(voter.Id))
        {
            await comp.FollowupAsync("Du bist kein Spieler in dieser Runde.", ephemeral: true);
            return;
        }

        if (extra is null || !ulong.TryParse(extra, out var targetId))
        {
            await comp.FollowupAsync("Ungültige Stimme.", ephemeral: true);
            return;
        }

        if (!Players.ContainsKey(targetId))
        {
            await comp.FollowupAsync("Dieser Spieler ist nicht mehr im Spiel.", ephemeral: true);
            return;
        }

        Votes[voter.Id] = targetId;

        var target = GetPlayer(targetId)!;
        await comp.FollowupAsync($"Du hast für **{target.DisplayName}** gestimmt. 🗳️", ephemeral: true);

        // Prüfen ob alle gevotet haben
        if (Votes.Count >= Players.Count)
            await ResolveVotingAsync();
    }

    private async Task ResolveVotingAsync()
    {
        // Mehrheit berechnen
        var voteCounts = Votes.Values
            .GroupBy(id => id)
            .Select(g => (PlayerId: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var majority = Players.Count / 2 + 1;
        var winner = voteCounts.FirstOrDefault();

        VotedPlayerId = winner.Count >= majority ? winner.PlayerId : null;

        IsFinished = true;

        var imposter = ImposterId is { } impId ? GetPlayer(impId) : null;
        var voted = VotedPlayerId is { } votedId ? GetPlayer(votedId) : null;

        bool imposterFound = VotedPlayerId == ImposterId;

        var resultEmbed = new EmbedBuilder()
            .WithTitle("🕵️ Imposter — Ergebnis")
            .WithColor(imposterFound ? Color.Green : Color.Red);

        var description = new List<string>();

        // Stimmen-Übersicht
        description.Add("**Abstimmungsergebnis:**");
        foreach (var (playerId, count) in voteCounts)
        {
            var p = GetPlayer(playerId);
            description.Add($"• {p?.DisplayName ?? "???"}: {count} Stimme(n)");
        }
        description.Add("");

        // Ergebnis
        if (imposterFound)
        {
            description.Add($"🎉 **{voted?.DisplayName}** wurde als Imposter enttarnt!");
            description.Add("");
            description.Add("**Die Spieler gewinnen!** 🏆");
        }
        else if (voted is not null)
        {
            description.Add($"❌ **{voted.DisplayName}** war **nicht** der Imposter.");
            description.Add("");
            description.Add("**Der Imposter gewinnt!** 🏆");
        }
        else
        {
            description.Add("🤷 **Keine Mehrheit** — der Imposter entkommt!");
            description.Add("");
            description.Add("**Der Imposter gewinnt!** 🏆");
        }

        // Auflösung
        description.Add("");
        description.Add("---");
        description.Add($"**Der Imposter war:** {imposter?.Mention ?? "???"} ({imposter?.DisplayName ?? "???"})");
        description.Add("");

        if (Mode == ImposterMode.Word)
        {
            description.Add($"**Wort der Spieler:** {PlayerContent}");
            description.Add($"**Wort des Imposters:** {ImposterContent}");
        }
        else
        {
            description.Add($"**Frage der Spieler:** {PlayerContent}");
            description.Add($"**Frage des Imposters:** {ImposterContent}");
        }

        resultEmbed.WithDescription(string.Join('\n', description));

        if (_gameMessage is not null)
        {
            await _gameMessage.ModifyAsync(p =>
            {
                p.Embed = resultEmbed.Build();
                p.Components = new ComponentBuilder().Build();
            });
        }
        else
        {
            await LobbyChannel.SendMessageAsync(embed: resultEmbed.Build());
        }

        _manager.Remove(this);
    }

    // ---------------------------------------------------------------- Question-Modus: Antwort einreichen (Legacy, nicht mehr genutzt)

    // (Die Methode SubmitAnswerAsync wurde durch HandleModalAsync ersetzt.)

    // ---------------------------------------------------------------- Question-Modus: Modal & Antwort

    /// <summary>Öffnet das Modal (Popup) zum Antworten.</summary>
    private async Task HandleAnswerButtonAsync(SocketMessageComponent comp)
    {
        if (comp.User is not SocketGuildUser user || !Players.ContainsKey(user.Id))
        {
            await comp.FollowupAsync("Du bist kein Spieler in dieser Runde.", ephemeral: true);
            return;
        }

        if (ImposterId is null)
        {
            await comp.FollowupAsync("Das Spiel wurde noch nicht gestartet.", ephemeral: true);
            return;
        }

        if (IsVoting)
        {
            await comp.FollowupAsync("Die Abstimmung läuft bereits — keine Antworten mehr möglich.", ephemeral: true);
            return;
        }

        var isImposter = user.Id == ImposterId;
        var question = isImposter ? ImposterContent : PlayerContent;

        var modal = new ModalBuilder()
            .WithTitle("Deine Antwort")
            .WithCustomId(ComponentId("modal"))
            .AddTextInput("Frage", "question_display", TextInputStyle.Paragraph,
                placeholder: question, value: question, required: false)
            .AddTextInput("Deine Antwort", "answer", TextInputStyle.Paragraph,
                placeholder: "Schreib hier deine Antwort...", required: true, maxLength: 500);

        await comp.RespondWithModalAsync(modal.Build());
    }

    /// <summary>Wird vom ImposterEvent aufgerufen, wenn ein Modal abgeschickt wird.</summary>
    public async Task HandleModalAsync(SocketModal modal)
    {
        if (modal.User is not SocketGuildUser user || !Players.ContainsKey(user.Id))
        {
            await modal.RespondAsync("Du bist kein Spieler in dieser Runde.", ephemeral: true);
            return;
        }

        if (IsFinished || IsVoting)
        {
            await modal.RespondAsync("Antworten sind nicht mehr möglich.", ephemeral: true);
            return;
        }

        var answerComponent = modal.Data.Components
            .FirstOrDefault(c => c.CustomId == "answer");

        if (answerComponent?.Value is not { } answer || string.IsNullOrWhiteSpace(answer))
        {
            await modal.RespondAsync("Bitte gib eine Antwort ein.", ephemeral: true);
            return;
        }

        Answers[user.Id] = answer.Trim();

        await modal.RespondAsync($"✅ Deine Antwort wurde eingereicht: **{answer.Trim()}**", ephemeral: true);

        // Spiel-Nachricht aktualisieren (Antworten-Zähler)
        if (_gameMessage is not null)
        {
            var gameEmbed = new EmbedBuilder()
                .WithTitle("🕵️ Imposter — Runde läuft!")
                .WithDescription(BuildGameDescription())
                .WithColor(Color.Gold)
                .WithFooter($"Modus: {(Mode == ImposterMode.Word ? "Wort" : "Frage")}")
                .Build();

            await _gameMessage.ModifyAsync(p => { p.Embed = gameEmbed; });
        }
    }

    // ---------------------------------------------------------------- Beschreibung während des Spiels

    private string BuildGameDescription()
    {
        var lines = new List<string>
        {
            $"**Sprachkanal:** {Voice.Name}",
            "",
            "**Spieler:**",
        };

        lines.AddRange(Players.Values.Select(u => $"• {u.Mention}"));
        lines.Add("");

        if (Mode == ImposterMode.Question)
        {
            var submitted = Answers.Count;
            lines.Add($"**Antworten eingereicht:** {submitted}/{Players.Count}");
            lines.Add("");
            lines.Add("*Klickt auf \"📝 Antwort abgeben\" um eure Antwort einzureichen.*");
        }
        else
        {
            lines.Add("*Ihr habt alle ein Wort per DM erhalten. Beschreibt euer Wort,*");
            lines.Add("*ohne es zu nennen. Einer von euch hat ein anderes Wort...*");
        }

        lines.Add("");
        lines.Add("*Der Host kann die Runde beenden, wenn alle fertig sind.*");

        return string.Join('\n', lines);
    }

    // ---------------------------------------------------------------- Debug

    /// <summary>
    /// Owner-only Debug-Ansicht: kompletter interner Spielzustand inkl.
    /// Imposter-Identität, geheimer Inhalte, Antworten und Stimmen.
    /// Nur ephemeral anzeigen!
    /// </summary>
    public Embed BuildDebugEmbed()
    {
        var phase = IsFinished ? "Beendet"
            : IsVoting ? "Voting"
            : ImposterId is null ? "Lobby"
            : "Runde läuft";

        var sb = new StringBuilder();
        sb.AppendLine($"**Modus:** {(Mode == ImposterMode.Word ? "Wort" : "Frage")} · **Phase:** {phase}");
        sb.AppendLine($"**Host:** {Host.Mention} · **Sprachkanal:** {Voice.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Spieler ({Players.Count}):** {(Players.Count == 0 ? "—" : string.Join(", ", Players.Values.Select(u => u.Mention)))}");
        sb.AppendLine($"**Imposter:** {(ImposterId is { } imp && Players.TryGetValue(imp, out var impUser) ? impUser.Mention : "—")}");
        sb.AppendLine($"**Inhalt Spieler:** {(string.IsNullOrEmpty(PlayerContent) ? "—" : PlayerContent)}");
        sb.AppendLine($"**Inhalt Imposter:** {(string.IsNullOrEmpty(ImposterContent) ? "—" : ImposterContent)}");

        if (Answers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"**Antworten ({Answers.Count}/{Players.Count}):**");
            foreach (var (userId, answer) in Answers)
                sb.AppendLine($"- {MentionOf(userId)}: {answer}");
        }

        if (Votes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Stimmen:**");
            foreach (var (voterId, targetId) in Votes)
                sb.AppendLine($"- 🗳️ {MentionOf(voterId)} → {MentionOf(targetId)}");
        }

        if (VotedPlayerId is { } voted)
            sb.AppendLine($"\n**Als Imposter gevotet:** {MentionOf(voted)}");

        return new EmbedBuilder()
            .WithTitle($"🔧 Imposter-Debug `{GameId}`")
            .WithDescription(sb.ToString())
            .WithColor(Color.Purple)
            .Build();

        string MentionOf(ulong id) => Players.TryGetValue(id, out var u) ? u.Mention : id.ToString();
    }

    // ---------------------------------------------------------------- DM-Helper

    public async Task<bool> TryDmAsync(SocketGuildUser user, Embed embed, MessageComponent? components = null)
    {
        try
        {
            var dm = await user.CreateDMChannelAsync();
            await dm.SendMessageAsync(embed: embed, components: components);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not DM {User} ({Id})", user.Username, user.Id);
            return false;
        }
    }

    // ---------------------------------------------------------------- JSON-Modelle

    private sealed class ImposterWordsAsset
    {
        public List<WordGroup> Groups { get; init; } = [];
    }

    private sealed class ImposterQuestionsAsset
    {
        public List<QuestionGroup> Groups { get; init; } = [];
    }

    private sealed class QuestionGroup
    {
        public string Name { get; init; } = "";
        public List<string> Questions { get; init; } = [];
    }

    private sealed class WordGroup
    {
        public string Name { get; init; } = "";
        public List<string> Words { get; init; } = [];
    }
}
