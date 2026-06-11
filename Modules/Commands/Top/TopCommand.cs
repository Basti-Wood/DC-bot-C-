using DCBot.Services.Level;
using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Top;

/// <summary>/top [seite] — Rangliste der aktivsten Mitglieder.</summary>
public sealed class TopCommand : ICommand
{
    public string Name => "top";
    public string Description => "Zeigt die Rangliste der aktivsten Mitglieder";
    public bool AllowEveryone => true;

    private readonly LevelRepository _repo;

    public TopCommand(LevelRepository repo)
    {
        _repo = repo;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("seite")
            .WithDescription("Seite der Rangliste (Standard: 1)")
            .WithType(ApplicationCommandOptionType.Integer)
            .WithMinValue(1)
            .WithRequired(false),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        await cmd.DeferAsync();

        if (cmd.GuildId is not { } guildId)
        {
            await cmd.FollowupAsync("Dieser Befehl funktioniert nur auf einem Server.", ephemeral: true);
            return;
        }

        const int pageSize = 10;
        var page = (int)(cmd.Data.Options.FirstOrDefault(o => o.Name == "seite")?.Value as long? ?? 1);
        if (page < 1) page = 1;

        var entries = await _repo.GetTopAsync(guildId, page, pageSize);
        var total = await _repo.CountAsync(guildId);

        if (entries.Count == 0)
        {
            await cmd.FollowupAsync("Diese Seite der Rangliste ist leer.");
            return;
        }

        var lines = new List<string>();
        var offset = (page - 1) * pageSize;
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var pos = offset + i + 1;
            var medal = pos switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"`#{pos}`" };
            lines.Add($"{medal} <@{e.UserId}> — Level **{XpMath.CalcLevel(e.Xp)}** ({e.Xp:N0} XP)");
        }

        var maxPage = Math.Max(1, (total + pageSize - 1) / pageSize);
        var embed = new EmbedBuilder()
            .WithTitle("🏆 Rangliste")
            .WithDescription(string.Join("\n", lines))
            .WithFooter($"Seite {page}/{maxPage} • {total} Mitglieder")
            .WithColor(Color.Gold)
            .Build();

        await cmd.FollowupAsync(embed: embed);
    }
}
