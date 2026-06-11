using DCBot.Services.Roleplay;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Rp;

/// <summary>
/// /rp — Roleplay-Reaktionen mit GIFs (Port von rp_command.go).
/// Ein Command mit 21 Subcommands (blush, hug, pat, slap, ...), jeweils mit
/// optionalem Ziel-User. GIFs kommen aus der OtakuGIFs- oder Basti-API.
/// </summary>
public sealed class RpCommand : ICommand
{
    public string Name => "rp";
    public string Description => "Roleplay-Reaktionen mit GIFs";
    public bool AllowEveryone => true;
    public int Cooldown => 3;

    // kind -> (Text mit Ziel-User, Text ohne Ziel) — 1:1 aus dem Go-Bot.
    // user1 = Auslöser, user2 = Ziel.
    private static readonly Dictionary<string, (string Text, string TextAll)> RolePlayMap = new()
    {
        ["blush"]    = ("user2, user1 errötet wegen dir!", "user1 errötet"),
        ["cheers"]   = ("user2, prosit von user1!", "user1 prost!"),
        ["cool"]     = ("user2, user1 findet dich cool!", "user1 ist cool"),
        ["cry"]      = ("user2, user1 weint wegen dir!", "user1 weint"),
        ["cuddle"]   = ("user2, user1 kuschelt mit dir!", "user1 kuschelt mit allen!"),
        ["facepalm"] = ("user2, user1 facepalmt wegen dir!", "user1 facepalm"),
        ["happy"]    = ("user2, user1 ist glücklich wegen dir!", "user1 ist glücklich"),
        ["hug"]      = ("user2, user1 umarmt dich!", "user1 umarmt alle!"),
        ["laugh"]    = ("user2, user1 lacht wegen dir!", "user1 lacht"),
        ["love"]     = ("user2, user1 liebt dich! <:LoveTuba:1090372406897561791>", "user1 liebt alle! <:LoveTuba:1090372406897561791>"),
        ["mad"]      = ("user2, user1 ist wütend auf dich!", "user1 ist wütend"),
        ["nervous"]  = ("user2, user1 ist nervös wegen dir!", "user1 ist nervös"),
        ["no"]       = ("user1 sagt nein zu user2!", "user1 NEIN!"),
        ["pat"]      = ("user2, user1 streichelt dich! <:PatpatTuba:1120695389973123253>", "user1 streichelt alle! <:PatpatTuba:1120695389973123253>"),
        ["sad"]      = ("user2, user1 ist traurig wegen dir! <:DepressedEMOTE:1226978508736172123>", "user1 ist traurig! <:DepressedEMOTE:1226978508736172123>"),
        ["scared"]   = ("user2, user1 hat Angst vor dir! <:tuubaa_w:1235347591709982813>", "user1 hat Angst! <:tuubaa_w:1235347591709982813>"),
        ["shy"]      = ("user2, user1 ist schüchtern wegen dir! <:tuubaa_verlegen_Emote:1236346476649513043>", "user1 ist schüchtern! <:tuubaa_verlegen_Emote:1236346476649513043>"),
        ["slap"]     = ("user2, user1 schlägt dich!", "user1 schlägt"),
        ["sleep"]    = ("user2, user1 schläft wegen dir! <:SleepTuba:1123745924720627722>", "user1 schläft! <:SleepTuba:1123745924720627722>"),
        ["smug"]     = ("user2, user1 ist zufrieden wegen dir!", "user1 ist zufrieden"),
        ["yay"]      = ("user2, user1 freut sich wegen dir!", "user1 freut sich"),
    };

    private static readonly uint[] AccentColors = { 0x3498db, 0xe74c3c, 0x9b59b6, 0x2ecc71 };

    private readonly RoleplayApi _api;
    private readonly ILogger<RpCommand> _logger;
    private readonly Random _random = new();

    public RpCommand(RoleplayApi api, ILogger<RpCommand> logger)
    {
        _api = api;
        _logger = logger;
    }

    public List<SlashCommandOptionBuilder> BuildOptions()
        => RolePlayMap.Keys
            .OrderBy(k => k)
            .Select(kind => new SlashCommandOptionBuilder()
                .WithName(kind)
                .WithDescription($"Reaktion: {kind}")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("user")
                    .WithDescription("Ziel-User (optional)")
                    .WithType(ApplicationCommandOptionType.User)
                    .WithRequired(false)))
            .ToList();

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        var sub = cmd.Data.Options.FirstOrDefault();
        if (sub is null || !RolePlayMap.TryGetValue(sub.Name, out var data))
        {
            await cmd.RespondAsync("Den Befehl gibt es irgendwie nicht.", ephemeral: true);
            return;
        }

        await cmd.DeferAsync(); // GIF fetch kann dauern

        var target = sub.Options.FirstOrDefault(o => o.Name == "user")?.Value as IUser;

        var gif = await _api.GetGifUrlAsync(sub.Name);
        if (string.IsNullOrEmpty(gif))
        {
            _logger.LogError("rp: failed to fetch gif kind={Kind}", sub.Name);
            await cmd.FollowupAsync("Fehler beim Laden des GIFs.", ephemeral: true);
            return;
        }

        var text = target is not null
            ? ReplaceOnce(ReplaceOnce(data.Text, "user1", cmd.User.Mention), "user2", target.Mention)
            : ReplaceOnce(data.TextAll, "user1", cmd.User.Mention);

        var accent = AccentColors[_random.Next(AccentColors.Length)];
        var displayName = (cmd.User as SocketGuildUser)?.DisplayName
                          ?? cmd.User.GlobalName ?? cmd.User.Username;

        var embed = new EmbedBuilder()
            .WithDescription(text)
            .WithImageUrl(gif)
            .WithColor(new Color(accent))
            .WithFooter($"Angefordert von {displayName}")
            .Build();

        await cmd.FollowupAsync(embed: embed);
    }

    private static string ReplaceOnce(string s, string oldValue, string newValue)
    {
        var idx = s.IndexOf(oldValue, StringComparison.Ordinal);
        return idx < 0 ? s : s[..idx] + newValue + s[(idx + oldValue.Length)..];
    }
}
