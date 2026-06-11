using DCBot.Services.Level;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.SetLevel;

/// <summary>/setlevel user level — Level direkt setzen (Admin).</summary>
public sealed class SetLevelCommand : ICommand
{
    public string Name => "setlevel";
    public string Description => "Setzt das Level eines Users direkt (Admin)";
    public bool AllowAdmin => true;

    private readonly LevelRepository _repo;
    private readonly ILogger<SetLevelCommand> _logger;

    public SetLevelCommand(LevelRepository repo, ILogger<SetLevelCommand> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("user")
            .WithDescription("Der User dessen Level gesetzt werden soll")
            .WithType(ApplicationCommandOptionType.User)
            .WithRequired(true),
        new SlashCommandOptionBuilder()
            .WithName("level")
            .WithDescription($"Level (0–{XpMath.MaxLevel})")
            .WithType(ApplicationCommandOptionType.Integer)
            .WithMinValue(0)
            .WithMaxValue(XpMath.MaxLevel)
            .WithRequired(true),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        if (cmd.GuildId is not { } guildId)
        {
            await cmd.RespondAsync("Dieser Befehl funktioniert nur auf einem Server.", ephemeral: true);
            return;
        }

        var target = (IUser)cmd.Data.Options.First(o => o.Name == "user").Value;
        var level = (int)(long)cmd.Data.Options.First(o => o.Name == "level").Value;

        var xp = XpMath.TotalXpForLevel(level);
        await _repo.SetXpAsync(guildId, target.Id, xp);

        _logger.LogInformation("setlevel: {User} -> level {Level} ({Xp} XP) by {Admin}",
            target.Id, level, xp, cmd.User.Id);

        await cmd.RespondAsync(
            $"{target.Mention} ist jetzt **Level {level}** ({xp:N0} XP).", ephemeral: true);
    }
}
