using System.Text;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Werwolf;

public enum GamePhase
{
    Lobby,
    Cupid,
    Night,
    Day,
    Ended,
}

public enum NeutralAlignment
{
    Village,
    Werewolf,
    Random,
}

/// <summary>Zustand einer laufenden Nacht (Zielauswahl der Rollen).</summary>
public sealed class NightState
{
    public required int Round { get; init; }

    /// <summary>Spieler, deren Nachtaktion bereits eingegangen ist.</summary>
    public HashSet<ulong> Submitted { get; } = new();

    /// <summary>Gewählte Ziele dieser Nacht: SpielerId -> ZielId.</summary>
    public Dictionary<ulong, ulong> Selections { get; } = new();

    /// <summary>Vom Schamanen beschützte Ziele (verhindern den Werwolf-Kill).</summary>
    public HashSet<ulong> Protected { get; } = new();
}

/// <summary>
/// Eine komplette Werwolf-Partie: Lobby, Rollenvergabe, Nacht-/Tag-Zyklus,
/// Werwolf-Chat und Siegbedingungen. Läuft komplett in-memory.
/// </summary>
public sealed class WerwolfGame
{
    public string GameId { get; } = Guid.NewGuid().ToString("N")[..8];
    public SocketGuild Guild { get; }
    public SocketTextChannel LobbyChannel { get; }
    public SocketGuildUser Host { get; }
    public SocketVoiceChannel Voice { get; }

    /// <summary>ID des Werwolf-Chats (null, solange der Kanal nicht existiert).</summary>
    public ulong? WolfChannelId => _wolfChannel?.Id;

    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    public int Round { get; private set; }

    public Dictionary<ulong, Player> Players { get; } = new();
    public int WerewolfCount { get; set; } = 2;
    public HashSet<string> BonusRoles { get; } = new();
    public NeutralAlignment NeutralSetting { get; set; } = NeutralAlignment.Random;

    private const int MinPlayers = 4;
    private const int MaxPlayers = 20;

    private readonly WerwolfGameManager _manager;
    private readonly ILogger<WerwolfGame> _logger;
    private readonly Random _rng = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IUserMessage? _lobbyMessage;
    private IUserMessage? _hostPanel;
    private IUserMessage? _voteMessage;
    private IUserMessage? _wolfNightMessage;
    private RestTextChannel? _wolfChannel;

    private NightState? _night;
    private string? _previousVoiceStatus;
    private readonly Dictionary<ulong, ulong> _wolfVotes = new(); // wolfId -> targetId
    private readonly Dictionary<ulong, ulong> _dayVotes = new();  // voterId -> targetId (0 = niemand)

    public WerwolfGame(
        WerwolfGameManager manager,
        SocketGuild guild,
        SocketTextChannel lobbyChannel,
        SocketGuildUser host,
        SocketVoiceChannel voice,
        ILogger<WerwolfGame> logger)
    {
        _manager = manager;
        Guild = guild;
        LobbyChannel = lobbyChannel;
        Host = host;
        Voice = voice;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Teams

    public Team GetTeam(Player p) => p.Role.IsNeutral ? p.NeutralTeam : p.Role.BaseTeam;

    private static string TeamText(Player p)
        => p.Role.IsNeutral ? (p.NeutralTeam == Team.Werewolf ? "Werwölfe" : "Dorf")
           : p.Role.BaseTeam == Team.Werewolf ? "Werwölfe" : "Dorf";

    public static string DisplayName(Player p) => p.User.DisplayName;

    private Player? GetPlayer(ulong userId) => Players.GetValueOrDefault(userId);

    /// <summary>Baut eine Component-CustomId (Format: ww:{GameId}:{action}[:extra]).</summary>
    public string ComponentId(string action, string? extra = null)
        => extra is null ? $"ww:{GameId}:{action}" : $"ww:{GameId}:{action}:{extra}";

    private string Id(string action, string? extra = null) => ComponentId(action, extra);

    // ---------------------------------------------------------------- Lobby

    /// <summary>Erstellt Lobby-Nachricht (öffentlich) + Host-Panel (ephemeral).</summary>
    public async Task BeginLobbyAsync(SocketSlashCommand cmd)
    {
        Players[Host.Id] = new Player { User = Host, Role = new VillagerRole() };

        _lobbyMessage = await cmd.FollowupAsync(embed: BuildLobbyEmbed(), components: BuildLobbyComponents());
        _hostPanel = await cmd.FollowupAsync(embed: BuildHostEmbed(), components: BuildHostComponents(), ephemeral: true);
    }

    private MessageComponent BuildLobbyComponents() =>
        new ComponentBuilder()
            .WithButton("🎭 Beitreten", Id("join"), ButtonStyle.Success)
            .Build();

    private MessageComponent BuildHostComponents()
    {
        var cb = new ComponentBuilder();

        cb.WithButton("➖", Id("w-"), ButtonStyle.Danger, row: 0);
        cb.WithButton($"{WerewolfCount} Werwölfe", Id("w+"), style: ButtonStyle.Secondary, row: 0, disabled: true);
        cb.WithButton("➕", Id("w+"), ButtonStyle.Success, row: 0);

        int row = 1, col = 0;
        foreach (var (key, label) in RoleRegistry.BonusRoles)
        {
            var on = BonusRoles.Contains(key);
            cb.WithButton($"{(on ? "✅" : "⬜")} {label}", Id("toggle", key),
                on ? ButtonStyle.Success : ButtonStyle.Secondary, row: row);
            if (++col == 3) { col = 0; row++; }
        }

        cb.WithButton($"⚖️ Neutrale: {NeutralLabel()}", Id("align"), ButtonStyle.Primary, row: 3);
        cb.WithButton("▶️ Start", Id("start"), ButtonStyle.Success, row: 4);
        cb.WithButton("🛑 Abbrechen", Id("cancel"), ButtonStyle.Danger, row: 4);
        return cb.Build();
    }

    private Embed BuildLobbyEmbed()
    {
        var sb = new StringBuilder();
        foreach (var p in Players.Values.OrderBy(p => p.User.Id))
            sb.AppendLine($"- {p.User.Mention}{(p.User.Id == Host.Id ? " *(Host)*" : "")}");

        return new EmbedBuilder()
            .WithTitle("🎭 Werwolf-Lobby")
            .WithDescription(
                $"Host: {Host.Mention}\nSprachkanal: {Voice.Mention}\n\n" +
                $"**Einstellungen:** 🐺 {WerewolfCount} Werwölfe · ✨ Bonusrollen: {BonusRoleSummary()} · ⚖️ Neutrale: {NeutralLabel()}\n\n" +
                $"**Spieler ({Players.Count}/{MaxPlayers}):**\n{(Players.Count == 0 ? "—" : sb.ToString())}")
            .WithColor(Color.Gold)
            .Build();
    }

    private Embed BuildHostEmbed()
    {
        var sb = new StringBuilder();
        foreach (var p in Players.Values.OrderBy(p => p.User.Id))
            sb.AppendLine($"- {p.User.Mention}");
        return new EmbedBuilder()
            .WithTitle("🎛️ Host-Einstellungen")
            .WithDescription($"Spieler ({Players.Count}/{MaxPlayers}):\n{sb}\nAlle im Sprachkanal können per „Beitreten“-Button mitmachen.")
            .WithColor(Color.Blue)
            .Build();
    }

    private string NeutralLabel() => NeutralSetting switch
    {
        NeutralAlignment.Village => "Dorf",
        NeutralAlignment.Werewolf => "Werwölfe",
        _ => "Zufall",
    };

    private string BonusRoleSummary()
        => BonusRoles.Count == 0 ? "keine" : string.Join(", ", BonusRoles.Select(RoleRegistry.LabelOf));

    private async Task RefreshLobbyAsync()
    {
        if (_lobbyMessage is not null)
            await TryModifyAsync(_lobbyMessage, m => m.Embed = BuildLobbyEmbed());
        if (_hostPanel is not null)
            await TryModifyAsync(_hostPanel, m => { m.Embed = BuildHostEmbed(); m.Components = BuildHostComponents(); });
    }

    // ---------------------------------------------------------------- Komponenten-Events

    public async Task HandleButtonAsync(SocketMessageComponent comp, string action, string? extra)
    {
        await _lock.WaitAsync();
        try
        {
            switch (action)
            {
                case "join": await JoinAsync(comp); break;
                case "w+": await ChangeWolfCountAsync(comp, +1); break;
                case "w-": await ChangeWolfCountAsync(comp, -1); break;
                case "toggle": await ToggleRoleAsync(comp, extra); break;
                case "align": await CycleAlignmentAsync(comp); break;
                case "start": await StartAsync(comp); break;
                case "cancel": await CancelAsync(comp); break;
                case "force": await ForceAsync(comp, extra); break;
                case "twin": await HandleTwinButtonAsync(comp); break;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task HandleSelectAsync(SocketMessageComponent comp, string action, string? extra)
    {
        await _lock.WaitAsync();
        try
        {
            if (!ulong.TryParse(comp.Data.Values.FirstOrDefault(), out var value))
            {
                await comp.FollowupAsync("Ungültige Auswahl.", ephemeral: true);
                return;
            }
            switch (action)
            {
                case "night": await SubmitNightAsync(comp, value); break;
                case "cupid": await SubmitSetupSelectAsync(comp, extra, value); break;
                case "vengeful": await SubmitDeathSelectAsync(comp, value); break;
                case "vote": await CastVoteAsync(comp, value); break;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Select der Spielstart-Aktion → an die Rolle delegiert (z.B. Amor).</summary>
    private async Task SubmitSetupSelectAsync(SocketMessageComponent comp, string? step, ulong value)
    {
        var player = GetPlayer(comp.User.Id);
        if (player is null) { await comp.FollowupAsync("Du bist nicht Teil dieses Spiels.", ephemeral: true); return; }
        await player.Role.OnSetupSelectAsync(this, player, comp, step ?? string.Empty, value);
    }

    /// <summary>Select nach dem Tod → an die Rolle delegiert (z.B. Rachsüchtig).</summary>
    private async Task SubmitDeathSelectAsync(SocketMessageComponent comp, ulong value)
    {
        var player = GetPlayer(comp.User.Id);
        if (player is null) { await comp.FollowupAsync("Du bist nicht Teil dieses Spiels.", ephemeral: true); return; }
        await player.Role.OnDeathSelectAsync(this, player, comp, value);
    }

    private async Task<bool> IsHostAsync(SocketMessageComponent comp)
    {
        if (comp.User.Id == Host.Id) return true;
        await comp.FollowupAsync("Nur der Host darf das.", ephemeral: true);
        return false;
    }

    // ---------------------------------------------------------------- Lobby-Aktionen

    private async Task JoinAsync(SocketMessageComponent comp)
    {
        if (comp.User is not SocketGuildUser user) return;
        if (Phase != GamePhase.Lobby) { await comp.FollowupAsync("Das Spiel läuft bereits.", ephemeral: true); return; }
        if (Players.ContainsKey(user.Id)) { await comp.FollowupAsync("Du bist schon dabei.", ephemeral: true); return; }
        if (user.VoiceChannel?.Id != Voice.Id)
        {
            await comp.FollowupAsync($"Du musst im Sprachkanal {Voice.Mention} sein, um beizutreten.", ephemeral: true);
            return;
        }
        if (Players.Count >= MaxPlayers) { await comp.FollowupAsync("Das Spiel ist voll (20 Spieler).", ephemeral: true); return; }

        Players[user.Id] = new Player { User = user, Role = new VillagerRole() };
        await RefreshLobbyAsync();
        await comp.FollowupAsync("Du bist dabei! 🎭", ephemeral: true);
    }

    private async Task ChangeWolfCountAsync(SocketMessageComponent comp, int delta)
    {
        if (!await IsHostAsync(comp)) return;
        if (Phase != GamePhase.Lobby) { await comp.FollowupAsync("Das Spiel läuft bereits.", ephemeral: true); return; }
        WerewolfCount = Math.Clamp(WerewolfCount + delta, 1, 4);
        await RefreshLobbyAsync();
        await comp.FollowupAsync($"Werwölfe: {WerewolfCount}", ephemeral: true);
    }

    private async Task ToggleRoleAsync(SocketMessageComponent comp, string? key)
    {
        if (!await IsHostAsync(comp)) return;
        if (Phase != GamePhase.Lobby) { await comp.FollowupAsync("Das Spiel läuft bereits.", ephemeral: true); return; }
        if (key is null || RoleRegistry.BonusRoles.All(r => r.Key != key))
        {
            await comp.FollowupAsync("Unbekannte Rolle.", ephemeral: true);
            return;
        }
        if (!BonusRoles.Remove(key)) BonusRoles.Add(key);
        await RefreshLobbyAsync();
        await comp.FollowupAsync($"{(BonusRoles.Contains(key) ? "✅" : "⬜")} {RoleRegistry.LabelOf(key)}", ephemeral: true);
    }

    private async Task CycleAlignmentAsync(SocketMessageComponent comp)
    {
        if (!await IsHostAsync(comp)) return;
        if (Phase != GamePhase.Lobby) { await comp.FollowupAsync("Das Spiel läuft bereits.", ephemeral: true); return; }
        NeutralSetting = NeutralSetting switch
        {
            NeutralAlignment.Village => NeutralAlignment.Werewolf,
            NeutralAlignment.Werewolf => NeutralAlignment.Random,
            _ => NeutralAlignment.Village,
        };
        await RefreshLobbyAsync();
        await comp.FollowupAsync($"Neutrale: {NeutralLabel()}", ephemeral: true);
    }

    private async Task CancelAsync(SocketMessageComponent comp)
    {
        if (!await IsHostAsync(comp)) return;
        if (Phase != GamePhase.Lobby) { await comp.FollowupAsync("Ein laufendes Spiel kann nicht mehr abgebrochen werden.", ephemeral: true); return; }
        Phase = GamePhase.Ended;
        await AnnounceAsync("🛑 Die Lobby wurde abgebrochen.");
        _manager.Remove(this);
        await comp.FollowupAsync("Abgebrochen.", ephemeral: true);
    }

    // ---------------------------------------------------------------- Spielstart

    private async Task StartAsync(SocketMessageComponent comp)
    {
        if (!await IsHostAsync(comp)) return;
        if (Phase != GamePhase.Lobby) { await comp.FollowupAsync("Das Spiel läuft bereits.", ephemeral: true); return; }
        if (Players.Count < MinPlayers)
        {
            await comp.FollowupAsync($"Es werden mindestens {MinPlayers} Spieler benötigt.", ephemeral: true);
            return;
        }

        var wolves = Math.Min(WerewolfCount, Players.Count / 2);
        AssignRoles(wolves);
        await CreateWolfChannelAsync();
        await LockVoiceAsync();
        await SendRoleDmsAsync();

        await AnnounceAsync(new EmbedBuilder()
            .WithTitle("🎭 Das Werwolf-Spiel beginnt!")
            .WithDescription($"{Players.Count} Spieler, {wolves} Werwölfe. Die Rollen wurden per DM verteilt. Viel Glück!\n🔒 Der Sprachkanal ist jetzt gesperrt.")
            .WithColor(Color.Gold)
            .Build());

        var setupPlayer = Players.Values.FirstOrDefault(p => p.Alive && p.Role.HasGameStartAction);
        if (setupPlayer is not null)
        {
            Phase = GamePhase.Cupid;
            await setupPlayer.Role.OnGameStartAsync(this, setupPlayer);
        }
        else
        {
            await BeginNightAsync(1);
        }

        await comp.FollowupAsync("Spiel gestartet! 🎭", ephemeral: true);
    }

    private void AssignRoles(int wolves)
    {
        var bag = new List<Role>();
        for (var i = 0; i < wolves; i++) bag.Add(new WerewolfRole());

        var bonus = BonusRoles.OrderBy(_ => _rng.Next()).ToList();
        var slots = Players.Count - wolves;
        foreach (var key in bonus.Take(slots)) bag.Add(RoleRegistry.Create(key));
        while (bag.Count < Players.Count) bag.Add(new VillagerRole());

        for (var i = bag.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        var idx = 0;
        foreach (var p in Players.Values)
        {
            p.Role = bag[idx++];
            if (p.Role.IsNeutral)
            {
                p.NeutralTeam = NeutralSetting switch
                {
                    NeutralAlignment.Village => Team.Village,
                    NeutralAlignment.Werewolf => Team.Werewolf,
                    _ => _rng.Next(2) == 0 ? Team.Village : Team.Werewolf,
                };
            }
        }
    }

    private async Task CreateWolfChannelAsync()
    {
        var overwrites = new List<Overwrite>
        {
            new(Guild.EveryoneRole.Id, PermissionTarget.Role,
                new OverwritePermissions(viewChannel: PermValue.Deny)),
            new(Guild.CurrentUser.Id, PermissionTarget.User,
                new OverwritePermissions(
                    viewChannel: PermValue.Allow,
                    sendMessages: PermValue.Allow,
                    readMessageHistory: PermValue.Allow,
                    manageChannel: PermValue.Allow)),
        };

        foreach (var p in Players.Values.Where(p => p.Role.IsWerewolf))
        {
            overwrites.Add(new(p.User.Id, PermissionTarget.User,
                new OverwritePermissions(
                    viewChannel: PermValue.Allow,
                    sendMessages: PermValue.Allow,
                    readMessageHistory: PermValue.Allow)));
        }

        _wolfChannel = await Guild.CreateTextChannelAsync($"🐺-werwolf-{GameId[..4]}", props =>
        {
            props.Topic = $"Werwolf-Spiel {GameId} — nur für Werwölfe (und ausgewählte Geister)";
            props.PermissionOverwrites = overwrites;
        });
    }

    private async Task SendRoleDmsAsync()
    {
        foreach (var p in Players.Values)
        {
            if (!await TryDmAsync(p.User, BuildRoleEmbed(p)))
                await AnnounceAsync($"⚠️ {p.User.Mention} konnte keine DM empfangen — bitte DMs aktivieren!");
        }
    }

    private Embed BuildRoleEmbed(Player p)
    {
        var team = GetTeam(p);
        var desc = new StringBuilder();
        desc.AppendLine($"**Team:** {(team == Team.Werewolf ? "Werwölfe 🐺" : "Dorf 🏡")}");
        desc.AppendLine($"**Fähigkeit:** {p.Role.AbilityText}");
        desc.AppendLine();
        desc.AppendLine("**Spieler:**");
        foreach (var q in Players.Values.OrderBy(q => q.User.Id))
            desc.AppendLine($"- {DisplayName(q)}{(q.User.Id == p.User.Id ? " *(du)*" : "")}");

        var eb = new EmbedBuilder()
            .WithTitle($"{p.Role.Emoji} Du bist: {p.Role.DisplayName}")
            .WithDescription(desc.ToString())
            .WithColor(team == Team.Werewolf ? Color.DarkRed : Color.Green);

        if (p.Role.IsWerewolf && _wolfChannel is not null)
            eb.AddField("Werwolf-Chat", _wolfChannel.Mention);
        return eb.Build();
    }

    // ---------------------------------------------------------------- Nacht

    internal async Task BeginNightAsync(int round)
    {
        Round = round;
        Phase = GamePhase.Night;
        _night = new NightState { Round = round };
        _wolfVotes.Clear();

        var aliveWolves = Players.Values.Count(p => p.Alive && p.Role.IsWerewolf);
        if (aliveWolves > 0 && _wolfChannel is not null)
            await SendWolfNightMessageAsync();

        foreach (var p in Players.Values.Where(p => p.Alive && p.Role.HasNightAction))
        {
            var targets = p.Role.GetNightTargets(this, p);
            if (targets.Count == 0) { _night.Submitted.Add(p.User.Id); continue; }
            var menu = BuildTargetMenu(Id("night", p.Role.Key), targets, p.Role.NightPrompt);
            await TryDmAsync(p.User, NightEmbed(p), new ComponentBuilder().WithSelectMenu(menu).Build());
        }

        await AnnounceAsync(new EmbedBuilder()
            .WithTitle($"🌙 Nacht {round}")
            .WithDescription("Alle schlafen ... Rollen mit Fähigkeiten handeln jetzt per DM. Die Werwölfe stimmen im Werwolf-Chat ab.")
            .WithColor(Color.DarkBlue)
            .Build(),
            new ComponentBuilder()
                .WithButton("🌅 Nacht beenden (Host)", Id("force", "night"), ButtonStyle.Danger)
                .Build());
    }

    private Embed NightEmbed(Player p) => new EmbedBuilder()
        .WithTitle($"{p.Role.Emoji} Nacht {Round}")
        .WithDescription(p.Role.NightPrompt)
        .WithColor(Color.DarkBlue)
        .Build();

    private async Task SendWolfNightMessageAsync()
    {
        if (_wolfChannel is null) return;
        var targets = Players.Values.Where(p => p.Alive && !p.Role.IsWerewolf).OrderBy(p => p.User.Id).ToList();

        var desc = new StringBuilder();
        desc.AppendLine($"**🌙 Nacht {Round} — wählt euer Opfer!**");
        desc.AppendLine();
        var votes = _wolfVotes
            .Where(kv => Players.TryGetValue(kv.Key, out var w) && w.Alive && Players.TryGetValue(kv.Value, out _))
            .ToList();
        if (votes.Count == 0) desc.AppendLine("Noch keine Stimmen abgegeben.");
        else
            foreach (var (wolfId, targetId) in votes)
                desc.AppendLine($"🐺 {Players[wolfId].User.Mention} → {Players[targetId].User.Mention}");

        var cb = new ComponentBuilder();
        if (targets.Count > 0)
            cb.WithSelectMenu(BuildTargetMenu(Id("night", "wolf"), targets, "Wähle dein Opfer"));

        var embed = new EmbedBuilder()
            .WithTitle("🐺 Werwolf-Abstimmung")
            .WithDescription(desc.ToString())
            .WithColor(Color.DarkRed)
            .Build();

        if (_wolfNightMessage is null)
            _wolfNightMessage = await _wolfChannel.SendMessageAsync(embed: embed, components: cb.Build());
        else
            await TryModifyAsync(_wolfNightMessage, m => { m.Embed = embed; m.Components = cb.Build(); });
    }

    private async Task SubmitNightAsync(SocketMessageComponent comp, ulong targetId)
    {
        var player = GetPlayer(comp.User.Id);
        if (player is null) { await comp.FollowupAsync("Du bist nicht Teil dieses Spiels.", ephemeral: true); return; }
        if (Phase != GamePhase.Night) { await comp.FollowupAsync("Gerade ist keine Nacht.", ephemeral: true); return; }
        if (!player.Alive) { await comp.FollowupAsync("Du bist tot — Geister handeln nicht.", ephemeral: true); return; }

        // Werwolf-Stimme ist ein kollektiver Spielmechanismus (bleibt in der Engine)
        if (player.Role.IsWerewolf)
        {
            if (!Players.TryGetValue(targetId, out var t) || !t.Alive || t.Role.IsWerewolf)
            {
                await comp.FollowupAsync("Ungültiges Ziel.", ephemeral: true);
                return;
            }
            _wolfVotes[player.User.Id] = targetId;
            await comp.FollowupAsync($"✅ Du stimmst dafür, {t.User.Mention} zu reißen.", ephemeral: true);
            await SendWolfNightMessageAsync();
            await TryResolveNightAsync();
            return;
        }

        if (!player.Role.HasNightAction)
        {
            await comp.FollowupAsync("Du hast keine Nachtaktion.", ephemeral: true);
            return;
        }

        var targets = player.Role.GetNightTargets(this, player);
        if (targets.All(t => t.User.Id != targetId))
        {
            await comp.FollowupAsync("Ungültiges Ziel.", ephemeral: true);
            return;
        }

        _night!.Submitted.Add(player.User.Id);
        _night.Selections[player.User.Id] = targetId;
        await player.Role.OnNightSubmittedAsync(this, player, _night, comp, targetId);
        await TryResolveNightAsync();
    }

    private async Task TryResolveNightAsync()
    {
        if (_night is null || Phase != GamePhase.Night) return;
        var aliveWolves = Players.Values.Count(p => p.Alive && p.Role.IsWerewolf);
        var votesFromAliveWolves = _wolfVotes.Count(kv => Players.TryGetValue(kv.Key, out var p) && p.Alive);
        var actorsDone = Players.Values
            .Where(p => p.Alive && p.Role.HasNightAction)
            .All(p => _night.Submitted.Contains(p.User.Id));
        if (votesFromAliveWolves >= aliveWolves && actorsDone)
            await ResolveNightAsync();
    }

    /// <summary>Löst die Nacht auf (Lock muss gehalten werden).</summary>
    private async Task ResolveNightAsync()
    {
        var night = _night;
        _night = null;
        if (night is null) return;

        // Rollen-Nachtaktionen in Reihenfolge ihrer Priorität auflösen
        foreach (var p in Players.Values
                     .Where(p => p.Alive && p.Role.HasNightAction)
                     .OrderBy(p => p.Role.NightPriority))
        {
            await p.Role.OnNightResolveAsync(this, p, night);
        }

        // Werwolf-Kill (Mehrheitsentscheid im Werwolf-Chat)
        var killId = ComputeWolfKill();
        if (killId is { } victimId && Players.TryGetValue(victimId, out var victim))
        {
            if (night.Protected.Contains(victimId))
            {
                await AnnounceAsync(new EmbedBuilder()
                    .WithTitle("🛡️ Gerettet!")
                    .WithDescription($"{victim.User.Mention} wurde angegriffen, aber der Schamane hat die Person beschützt!")
                    .WithColor(Color.Green)
                    .Build());
            }
            else
            {
                await KillAsync(victim, "wurde in der Nacht von den Werwölfen gerissen 🐺");
            }
        }
        else
        {
            await AnnounceAsync(new EmbedBuilder()
                .WithTitle($"🌅 Nacht {Round} vorbei")
                .WithDescription("Niemand wurde in dieser Nacht getötet.")
                .WithColor(Color.DarkBlue)
                .Build());
        }

        if (await CheckWinAsync()) return;
        await BeginDayAsync();
    }

    private ulong? ComputeWolfKill()
    {
        var tally = new Dictionary<ulong, int>();
        foreach (var (wolfId, targetId) in _wolfVotes)
            if (Players.TryGetValue(wolfId, out var w) && w.Alive)
                tally[targetId] = tally.GetValueOrDefault(targetId) + 1;

        var max = 0;
        ulong? result = null;
        var tie = false;
        foreach (var kv in tally)
        {
            if (kv.Value > max) { max = kv.Value; result = kv.Key; tie = false; }
            else if (kv.Value == max && max > 0) { tie = true; result = null; }
        }
        return tie ? null : result;
    }

    // ---------------------------------------------------------------- Tod

    /// <summary>
    /// Tötet einen Spieler inkl. Liebeskummer-Kette. Rollen reagieren
    /// über Role.OnDeathAsync (Werwolf-Geist, Rachsucht, ...).
    /// </summary>
    public async Task KillAsync(Player player, string cause)
    {
        if (!player.Alive) return;
        player.Alive = false;

        await AnnounceAsync(new EmbedBuilder()
            .WithTitle("💀 Tod")
            .WithDescription($"{player.User.Mention} {cause}")
            .WithColor(Color.DarkRed)
            .Build());

        // Rollen-Reaktion auf den Tod (Werwolf-Geist, Rachsucht, ...)
        await player.Role.OnDeathAsync(this, player);

        // Liebeskummer
        if (player.Lover is { } lover && lover.Alive)
        {
            await TryDmAsync(lover.User, new EmbedBuilder()
                .WithTitle("💔 Liebeskummer")
                .WithDescription($"{player.User.Mention} ist gestorben ... du stirbst mit der Person aus Liebeskummer.")
                .WithColor(Color.Magenta)
                .Build());
            await KillAsync(lover, "stirbt an gebrochenem Herzen 💔");
        }
    }

    /// <summary>Setzt den Werwolf-Chat-Zugriff eines (toten) Spielers (nur lesen).</summary>
    public async Task SetWolfGhostAsync(Player p, bool allowRead)
    {
        if (_wolfChannel is null) return;
        try
        {
            await _wolfChannel.AddPermissionOverwriteAsync(p.User, new OverwritePermissions(
                viewChannel: allowRead ? PermValue.Allow : PermValue.Deny,
                sendMessages: PermValue.Deny,
                readMessageHistory: allowRead ? PermValue.Allow : PermValue.Deny));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update wolf channel permissions for {User}", p.User.Id);
        }
    }

    // ---------------------------------------------------------------- Tag

    private async Task BeginDayAsync()
    {
        Phase = GamePhase.Day;
        _dayVotes.Clear();

        // Friendzone-Stummschaltungen aktivieren (Sprach-Mute, nur für Lebende)
        foreach (var p in Players.Values.Where(p => p.MutedForRound && p.Alive))
            await TryVoiceMuteAsync(p.User, true);

        // Tagesaktionen der Rollen (z.B. Zwilling kann sich aufdecken)
        foreach (var p in Players.Values.Where(p => p.Alive))
            await p.Role.OnDayStartAsync(this, p);

        await SendDayMessageAsync();
    }

    private async Task SendDayMessageAsync()
    {
        var alive = Players.Values.Where(p => p.Alive).OrderBy(p => p.User.Id).ToList();
        var cb = new ComponentBuilder();
        if (alive.Count > 0)
            cb.WithSelectMenu(BuildTargetMenu(Id("vote"), alive, "Stimme für einen Spieler ab", includeNobody: true), 0);
        cb.WithButton("🔚 Abstimmung beenden (Host)", Id("force", "day"), ButtonStyle.Danger, row: 1);

        if (_voteMessage is null)
            _voteMessage = await LobbyChannel.SendMessageAsync(embed: BuildDayEmbed(), components: cb.Build());
        else
            await TryModifyAsync(_voteMessage, m => { m.Embed = BuildDayEmbed(); m.Components = cb.Build(); });
    }

    private Embed BuildDayEmbed()
    {
        var desc = new StringBuilder();
        desc.AppendLine($"**☀️ Tag {Round} — diskutiert und stimmt ab!**");
        desc.AppendLine();
        var tally = ComputeDayTally();
        if (tally.Count == 0) desc.AppendLine("Noch keine Stimmen.");
        else
            foreach (var (targetId, count) in tally.OrderByDescending(t => t.Value))
                desc.AppendLine(targetId == 0
                    ? $"🗳️ Niemand — {count}"
                    : $"🗳️ {Players[targetId].User.Mention} — {count}");

        return new EmbedBuilder()
            .WithTitle("🗳️ Abstimmung")
            .WithDescription(desc.ToString())
            .WithColor(Color.Gold)
            .Build();
    }

    private Dictionary<ulong, int> ComputeDayTally()
    {
        var tally = new Dictionary<ulong, int>();
        foreach (var (voterId, targetId) in _dayVotes)
        {
            if (!Players.TryGetValue(voterId, out var voter)) continue;
            tally[targetId] = tally.GetValueOrDefault(targetId) + voter.Role.VoteWeight;
        }
        return tally;
    }

    private async Task CastVoteAsync(SocketMessageComponent comp, ulong targetId)
    {
        var player = GetPlayer(comp.User.Id);
        if (player is null) { await comp.FollowupAsync("Du bist nicht Teil dieses Spiels.", ephemeral: true); return; }
        if (Phase != GamePhase.Day) { await comp.FollowupAsync("Gerade ist keine Abstimmung.", ephemeral: true); return; }
        if (!player.Alive) { await comp.FollowupAsync("Geister dürfen nicht abstimmen.", ephemeral: true); return; }
        if (player.MutedForRound)
        {
            await comp.FollowupAsync("Du wurdest stummgeschaltet und darfst nicht abstimmen. 🙊", ephemeral: true);
            return;
        }
        if (targetId != 0 && (!Players.TryGetValue(targetId, out var t) || !t.Alive))
        {
            await comp.FollowupAsync("Ungültiges Ziel.", ephemeral: true);
            return;
        }

        _dayVotes[player.User.Id] = targetId;
        var votedFor = targetId == 0 ? "Niemand" : Players[targetId].User.Mention;
        await comp.FollowupAsync($"✅ Du stimmst für {votedFor}.", ephemeral: true);
        await SendDayMessageAsync();
        await TryResolveDayAsync();
    }

    private async Task TryResolveDayAsync()
    {
        if (Phase != GamePhase.Day) return;
        var voters = Players.Values.Where(p => p.Alive && !p.MutedForRound).Select(p => p.User.Id).ToList();
        if (voters.All(id => _dayVotes.ContainsKey(id)))
            await ResolveDayAsync();
    }

    /// <summary>Wertet die Abstimmung aus (Lock muss gehalten werden).</summary>
    private async Task ResolveDayAsync()
    {
        if (Phase != GamePhase.Day) return;
        var tally = ComputeDayTally();

        var max = 0;
        ulong? winner = null;
        var tie = false;
        foreach (var (targetId, count) in tally)
        {
            if (count > max) { max = count; winner = targetId; tie = false; }
            else if (count == max && max > 0) { tie = true; winner = null; }
        }

        if (!tie && winner is { } lynchedId && lynchedId != 0 && Players.TryGetValue(lynchedId, out var lynched))
        {
            await KillAsync(lynched, $"wurde vom Dorf gelyncht 🪓 (Stimmen: {max})");
        }
        else
        {
            await AnnounceAsync(new EmbedBuilder()
                .WithTitle("🤝 Kein Urteil")
                .WithDescription(tally.Count == 0
                    ? "Niemand hat abgestimmt — niemand wird gelyncht."
                    : "Stimmengleichstand oder nur Enthaltungen — niemand wird gelyncht.")
                .WithColor(Color.LightGrey)
                .Build());
        }

        _dayVotes.Clear();
        foreach (var p in Players.Values.Where(p => p.MutedForRound))
        {
            p.MutedForRound = false;
            await TryVoiceMuteAsync(p.User, false);
        }

        if (await CheckWinAsync()) return;
        await BeginNightAsync(Round + 1);
    }

    private async Task HandleTwinButtonAsync(SocketMessageComponent comp)
    {
        var player = GetPlayer(comp.User.Id);
        if (player is null) { await comp.FollowupAsync("Du bist nicht Teil dieses Spiels.", ephemeral: true); return; }
        await player.Role.OnButtonAsync(this, player, comp, "twin");
    }

    // ---------------------------------------------------------------- Host-Force / Ende

    private async Task ForceAsync(SocketMessageComponent comp, string? phase)
    {
        if (!await IsHostAsync(comp)) return;
        switch (phase)
        {
            case "cupid":
                if (Phase != GamePhase.Cupid) { await comp.FollowupAsync("Amor hat bereits gewählt.", ephemeral: true); return; }
                await AnnounceAsync("💘 Der Host hat Amors Wahl übersprungen.");
                await BeginNightAsync(1);
                await comp.FollowupAsync("Amor übersprungen — Nacht 1 beginnt.", ephemeral: true);
                break;
            case "night":
                if (Phase != GamePhase.Night) { await comp.FollowupAsync("Gerade ist keine Nacht.", ephemeral: true); return; }
                await ResolveNightAsync();
                await comp.FollowupAsync("Die Nacht wurde beendet.", ephemeral: true);
                break;
            case "day":
                if (Phase != GamePhase.Day) { await comp.FollowupAsync("Gerade ist keine Abstimmung.", ephemeral: true); return; }
                await ResolveDayAsync();
                await comp.FollowupAsync("Die Abstimmung wurde beendet.", ephemeral: true);
                break;
        }
    }

    public async Task<bool> CheckWinAsync()
    {
        if (Phase == GamePhase.Ended) return true;
        var alive = Players.Values.Where(p => p.Alive).ToList();
        var wolves = alive.Count(p => GetTeam(p) == Team.Werewolf);
        var villagers = alive.Count(p => GetTeam(p) == Team.Village);

        if (wolves == 0) { await EndGameAsync(Team.Village); return true; }
        if (villagers == 0 || wolves >= villagers) { await EndGameAsync(Team.Werewolf); return true; }
        return false;
    }

    private async Task EndGameAsync(Team winner)
    {
        Phase = GamePhase.Ended;

        // Sprachkanal entsperren + Status zurücksetzen
        await UnlockVoiceAsync();

        // Sicherstellen, dass nach Spielende niemand mehr stummgeschaltet ist
        foreach (var p in Players.Values.Where(p => p.MutedForRound))
        {
            p.MutedForRound = false;
            await TryVoiceMuteAsync(p.User, false);
        }

        if (_wolfChannel is not null)
        {
            try { await _wolfChannel.DeleteAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete wolf channel"); }
            _wolfChannel = null;
        }

        var winnerText = winner == Team.Werewolf ? "Werwölfe 🐺" : "Dorfbewohner 🏡";
        var lines = new StringBuilder();
        foreach (var p in Players.Values.OrderBy(p => p.User.Id))
            lines.AppendLine($"{(p.Alive ? "🧍" : "💀")} {p.User.Mention} — **{p.Role.DisplayName}** ({TeamText(p)})");

        await AnnounceAsync(new EmbedBuilder()
            .WithTitle($"🏆 Die {winnerText} haben gewonnen!")
            .WithDescription(lines.ToString())
            .WithColor(winner == Team.Werewolf ? Color.DarkRed : Color.Green)
            .Build());

        _manager.Remove(this);
    }

    // ---------------------------------------------------------------- Helfer

    public SelectMenuBuilder BuildTargetMenu(string customId, List<Player> targets, string placeholder, bool includeNobody = false)
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithPlaceholder(placeholder)
            .WithMinValues(1)
            .WithMaxValues(1);
        foreach (var t in targets)
            menu.AddOption(DisplayName(t), t.User.Id.ToString());
        if (includeNobody)
            menu.AddOption("Niemand", "0", "Enthaltung / niemanden lynchen");
        return menu;
    }

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

    private async Task TryVoiceMuteAsync(SocketGuildUser user, bool mute)
    {
        try { await user.ModifyAsync(p => p.Mute = mute); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not (un)mute {User} in voice", user.Id); }
    }

    /// <summary>Sperrt den Sprachkanal (kein Beitritt mehr) und setzt den Status.</summary>
    private async Task LockVoiceAsync()
    {
        _previousVoiceStatus = Voice.Status;
        try { await Voice.SetStatusAsync("Spielen werwolf 🐺"); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not set voice channel status"); }
        try
        {
            await Voice.AddPermissionOverwriteAsync(Guild.EveryoneRole,
                new OverwritePermissions(connect: PermValue.Deny));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not lock voice channel"); }
    }

    /// <summary>Entsperrt den Sprachkanal und stellt den vorherigen Status wieder her.</summary>
    private async Task UnlockVoiceAsync()
    {
        try { await Voice.RemovePermissionOverwriteAsync(Guild.EveryoneRole); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not unlock voice channel"); }
        try
        {
            await Voice.SetStatusAsync(string.IsNullOrEmpty(_previousVoiceStatus) ? "" : _previousVoiceStatus);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not restore voice channel status"); }
    }

    private static async Task TryModifyAsync(IUserMessage message, Action<MessageProperties> changes)
    {
        try { await message.ModifyAsync(changes); }
        catch { /* Nachricht existiert evtl. nicht mehr */ }
    }

    /// <summary>Bearbeitet die Nachricht, an der das Component hing (funktioniert in Guild-Channels und DMs).</summary>
    public static async Task EditSourceMessageAsync(SocketMessageComponent comp, Action<MessageProperties> changes)
    {
        try
        {
            if (comp.Channel is ITextChannel tc) await tc.ModifyMessageAsync(comp.Message.Id, changes);
            else if (comp.Channel is IDMChannel dm) await dm.ModifyMessageAsync(comp.Message.Id, changes);
        }
        catch { /* Nachricht existiert evtl. nicht mehr */ }
    }

    public async Task AnnounceAsync(Embed embed, MessageComponent? components = null)
    {
        try { await LobbyChannel.SendMessageAsync(embed: embed, components: components); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not announce to lobby channel"); }
    }

    public async Task AnnounceAsync(string text)
    {
        try { await LobbyChannel.SendMessageAsync(text); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not announce to lobby channel"); }
    }

    // ---------------------------------------------------------------- Debug

    /// <summary>
    /// Owner-only Debug-Ansicht: kompletter interner Spielzustand inkl.
    /// aller Rollen, Teams, Nachtaktionen und Stimmen. Nur ephemeral anzeigen!
    /// </summary>
    public Embed BuildDebugEmbed()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"**Phase:** {Phase} · **Runde:** {Round}");
        sb.AppendLine($"**Einstellungen:** 🐺 {WerewolfCount} Werwölfe · ✨ {BonusRoleSummary()} · ⚖️ Neutrale: {NeutralLabel()}");
        sb.AppendLine($"**Host:** {Host.Mention} · **Sprachkanal:** {Voice.Mention}");
        sb.AppendLine($"**Werwolf-Chat:** {(_wolfChannel is null ? "—" : _wolfChannel.Mention)}");
        sb.AppendLine();

        sb.AppendLine($"**Spieler ({Players.Count}):**");
        foreach (var p in Players.Values.OrderBy(p => p.User.Id))
        {
            var flags = new List<string>();
            if (!p.Alive) flags.Add("💀");
            if (p.MutedForRound) flags.Add("🙊 stumm");
            if (p.Lover is not null) flags.Add($"💘 {DisplayName(p.Lover)}");
            sb.AppendLine($"- {p.User.Mention} — {p.Role.Emoji} **{p.Role.DisplayName}** ({TeamText(p)})" +
                          (flags.Count > 0 ? $" · {string.Join(" · ", flags)}" : ""));
        }

        if (_night is { } night)
        {
            sb.AppendLine();
            sb.AppendLine($"**Nacht {night.Round}:**");
            sb.AppendLine($"- Eingegangen: {(night.Submitted.Count == 0 ? "—" : string.Join(", ", night.Submitted.Select(Name)))}");
            sb.AppendLine($"- Auswahl: {(night.Selections.Count == 0 ? "—" : string.Join(", ", night.Selections.Select(kv => $"{Name(kv.Key)} → {Name(kv.Value)}")))}");
            sb.AppendLine($"- Geschützt: {(night.Protected.Count == 0 ? "—" : string.Join(", ", night.Protected.Select(Name)))}");
        }

        if (_wolfVotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Werwolf-Stimmen:**");
            foreach (var (wolfId, targetId) in _wolfVotes)
                sb.AppendLine($"- 🐺 {Name(wolfId)} → {Name(targetId)}");
        }

        if (_dayVotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Tages-Stimmen:**");
            foreach (var (voterId, targetId) in _dayVotes)
                sb.AppendLine($"- 🗳️ {Name(voterId)} → {(targetId == 0 ? "Niemand" : Name(targetId))}");
        }

        return new EmbedBuilder()
            .WithTitle($"🔧 Werwolf-Debug `{GameId}`")
            .WithDescription(sb.ToString())
            .WithColor(Color.Purple)
            .Build();

        string Name(ulong id) => Players.TryGetValue(id, out var pl) ? DisplayName(pl) : id.ToString();
    }
}
