using DCBot.Core;
using DCBot.Core.Database;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DCBot.Modules.Level;

/// <summary>
/// Level module: /level, /top, /setlevel + message XP event.
/// Port of the Go bot's modules/level.
/// </summary>
public sealed class LevelModule : IModule
{
    public string Name => "Level";

    private readonly LevelRepository _repo;
    private readonly GuildConfigService _guildConfig;
    private readonly ILogger<LevelModule> _logger;

    // Modules only request services registered in Program.cs
    // (DbContextFactory, BotConfig, GuildConfigService, loggers, client).
    // Module-internal helpers like LevelRepository are constructed here, so
    // adding a new module never requires touching Program.cs.
    public LevelModule(
        IDbContextFactory<BotDbContext> dbFactory,
        BotConfig config,
        GuildConfigService guildConfig,
        ILogger<LevelModule> logger)
    {
        _repo = new LevelRepository(dbFactory, config);
        _guildConfig = guildConfig;
        _logger = logger;
    }

    public void Register(CommandManager commands, EventManager events)
    {
        commands.Register(new SlashCommand
        {
            Name = "level",
            Description = "Zeigt dein Level oder das eines anderen Benutzers an",
            AllowEveryone = true,
            Options =
            {
                new SlashCommandOptionBuilder()
                    .WithName("user")
                    .WithDescription("Benutzer (optional)")
                    .WithType(ApplicationCommandOptionType.User)
                    .WithRequired(false),
            },
            Handler = LevelCommandAsync,
        });

        commands.Register(new SlashCommand
        {
            Name = "top",
            Description = "Zeigt die Rangliste der aktivsten Mitglieder",
            AllowEveryone = true,
            Options =
            {
                new SlashCommandOptionBuilder()
                    .WithName("seite")
                    .WithDescription("Seite der Rangliste (Standard: 1)")
                    .WithType(ApplicationCommandOptionType.Integer)
                    .WithMinValue(1)
                    .WithRequired(false),
            },
            Handler = TopCommandAsync,
        });

        commands.Register(new SlashCommand
        {
            Name = "setlevel",
            Description = "Setzt das Level eines Users direkt (Admin)",
            AllowAdmin = true,
            Options =
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
            },
            Handler = SetLevelCommandAsync,
        });

        // Message XP event
        var xpHandler = new MessageXpHandler(_repo, _guildConfig, _logger);
        events.OnMessageReceived(xpHandler.HandleAsync);
    }

    // ---------- /level ----------
    private async Task LevelCommandAsync(SocketSlashCommand cmd)
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
        var filled = (int)Math.Round(progress * barLength);
        filled = Math.Clamp(filled, 0, barLength);
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

    // ---------- /top ----------
    private async Task TopCommandAsync(SocketSlashCommand cmd)
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

    // ---------- /setlevel ----------
    private async Task SetLevelCommandAsync(SocketSlashCommand cmd)
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
