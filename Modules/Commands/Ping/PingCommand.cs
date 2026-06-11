using System.Diagnostics;
using DCBot.Core;
using DCBot.Core.Database;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace DCBot.Commands.Ping;

/// <summary>
/// /ping — Gateway- und DB-Latenz.
/// Kleinstes vollständiges Command — als Vorlage für neue Commands nutzen.
/// </summary>
public sealed class PingCommand : ICommand
{
    public string Name => "ping";
    public string Description => "Ping the bot and database";
    public bool AllowEveryone => true;
    public int Cooldown => 5;

    private readonly DiscordSocketClient _client;
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly BotConfig _config;

    public PingCommand(
        DiscordSocketClient client,
        IDbContextFactory<BotDbContext> dbFactory,
        BotConfig config)
    {
        _client = client;
        _dbFactory = dbFactory;
        _config = config;
    }

    public async Task ExecuteAsync(SocketSlashCommand cmd)
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
