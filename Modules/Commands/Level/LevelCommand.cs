using DCBot.Services.Level;
using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Level;

/// <summary>/level [user] — Level, Rang, XP + Fortschrittsbalken.</summary>
public sealed class LevelCommand : ICommand
{
    public string Name => "level";
    public string Description => "Zeigt dein Level oder das eines anderen Benutzers an";
    public bool AllowEveryone => true;

    private readonly LevelRepository _repo;

    public LevelCommand(LevelRepository repo)
    {
        _repo = repo;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("user")
            .WithDescription("Benutzer (optional)")
            .WithType(ApplicationCommandOptionType.User)
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

        var target = cmd.Data.Options.FirstOrDefault(o => o.Name == "user")?.Value as IUser
                     ?? cmd.User;

        var xp = await _repo.GetXpAsync(guildId, target.Id);
        var rank = await _repo.GetRankAsync(guildId, target.Id);
        var level = XpMath.CalcLevel(xp);
        var inLevel = XpMath.XpIntoCurrentLevel(xp);
        var needed = XpMath.XpForLevel(level);

        var progress = needed > 0 ? (double)inLevel / needed : 1.0;
        const int barLength = 20;
        var filled = Math.Clamp((int)Math.Round(progress * barLength), 0, barLength);
        var bar = new string('█', filled) + new string('░', barLength - filled);

        var embed = new EmbedBuilder()
            .WithAuthor(target.GlobalName ?? target.Username,
                        target.GetAvatarUrl() ?? target.GetDefaultAvatarUrl())
            .WithColor(Color.Blue)
            .AddField("Level", $"**{level}**", inline: true)
            .AddField("Rang", $"#{rank}", inline: true)
            .AddField("XP", $"{xp:N0}", inline: true)
            .AddField($"Fortschritt ({inLevel:N0} / {needed:N0} XP)", $"`{bar}`")
            .Build();

        await cmd.FollowupAsync(embed: embed);
    }
}
