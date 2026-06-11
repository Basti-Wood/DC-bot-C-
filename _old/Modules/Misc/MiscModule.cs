using System.Diagnostics;
using DCBot.Core;
using DCBot.Core.Database;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace DCBot.Modules.Misc;

/// <summary>
/// Misc module: /ping (gateway latency + database round trip).
/// Smallest possible example of a module — copy this folder as a
/// template when creating new modules.
/// </summary>
public sealed class MiscModule : IModule
{
    public string Name => "Misc";

    private readonly DiscordSocketClient _client;
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly BotConfig _config;

    public MiscModule(
        DiscordSocketClient client,
        IDbContextFactory<BotDbContext> dbFactory,
        BotConfig config)
    {
        _client = client;
        _dbFactory = dbFactory;
        _config = config;
    }

    public void Register(CommandManager commands, EventManager events)
    {
        commands.Register(new SlashCommand
        {
            Name = "ping",
            Description = "Ping the bot and database",
            AllowEveryone = true,
            Cooldown = 5,
            Handler = PingAsync,
        });
    }

    private async Task PingAsync(SocketSlashCommand cmd)
    {
        await cmd.DeferAsync(ephemeral: true);

        var sw = Stopwatch.StartNew();
        string dbStatus;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            _ = await db.UserLevels.AsNoTracking().CountAsync();
            sw.Stop();
            dbStatus = $"✅ {sw.ElapsedMilliseconds} ms";
        }
        catch (Exception ex)
        {
            sw.Stop();
            dbStatus = $"❌ {ex.Message}";
        }

        var embed = new EmbedBuilder()
            .WithTitle("🏓 Pong!")
            .AddField("Bot", $"`{_config.BotId}`", inline: true)
            .AddField("Gateway", $"{_client.Latency} ms", inline: true)
            .AddField("Database", dbStatus, inline: true)
            .WithColor(Color.Green)
            .Build();

        await cmd.FollowupAsync(embed: embed, ephemeral: true);
    }
}
