using DCBot.Core.Database;
using DCBot.Core.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCBot.Core;

/// <summary>
/// Shared access to per-guild key/value config (channels, roles, flags).
/// Used by any module that needs guild configuration.
/// All reads/writes are scoped to this bot's BotId.
/// </summary>
public sealed class GuildConfigService
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly BotConfig _config;

    public GuildConfigService(IDbContextFactory<BotDbContext> dbFactory, BotConfig config)
    {
        _dbFactory = dbFactory;
        _config = config;
    }

    public async Task<string?> GetAsync(ulong guildId, string key)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.GuildConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.BotId == _config.BotId && c.GuildId == guildId && c.Key == key);
        return entry?.Value;
    }

    public async Task<ulong?> GetChannelAsync(ulong guildId, string which)
    {
        var value = await GetAsync(guildId, $"channel:{which}");
        return ulong.TryParse(value, out var id) ? id : null;
    }

    public async Task SetAsync(ulong guildId, string key, string value)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.GuildConfigs
            .FirstOrDefaultAsync(c => c.BotId == _config.BotId && c.GuildId == guildId && c.Key == key);

        if (entry is null)
        {
            db.GuildConfigs.Add(new GuildConfig
            {
                BotId = _config.BotId,
                GuildId = guildId,
                Key = key,
                Value = value,
            });
        }
        else
        {
            entry.Value = value;
        }

        await db.SaveChangesAsync();
    }
}
