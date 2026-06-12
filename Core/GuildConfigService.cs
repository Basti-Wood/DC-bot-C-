using DCBot.Core.Database;
using DCBot.Core.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCBot.Core;

/// <summary>
/// Shared access to per-guild key/value config. All scoped to this BotId.
/// Keys:  channel:&lt;which&gt;   role:&lt;key&gt;   levelrole:&lt;threshold&gt;
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

    // ---------- channels ----------

    public async Task<ulong?> GetChannelAsync(ulong guildId, string which)
    {
        var value = await GetAsync(guildId, $"channel:{which}");
        return ulong.TryParse(value, out var id) ? id : null;
    }

    /// <summary>Die konfigurierten Art-Channels (art_1..art_3) der Guild.</summary>
    public async Task<List<ulong>> GetArtChannelsAsync(ulong guildId)
    {
        var result = new List<ulong>();
        foreach (var key in new[] { "art_1", "art_2", "art_3" })
        {
            if (await GetChannelAsync(guildId, key) is { } id)
                result.Add(id);
        }
        return result;
    }

    /// <summary>
    /// Alle Guilds dieses Bots, die einen bestimmten Channel konfiguriert
    /// haben (z.B. "main" für die Gute-Nacht-Nachricht).
    /// </summary>
    public async Task<List<(ulong GuildId, ulong ChannelId)>> GetGuildsWithChannelAsync(string which)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var key = $"channel:{which}";
        var entries = await db.GuildConfigs.AsNoTracking()
            .Where(c => c.BotId == _config.BotId && c.Key == key)
            .ToListAsync();

        return entries
            .Where(e => ulong.TryParse(e.Value, out _))
            .Select(e => (e.GuildId, ulong.Parse(e.Value)))
            .ToList();
    }

    // ---------- roles ----------

    public async Task<ulong?> GetRoleAsync(ulong guildId, string key)
    {
        var value = await GetAsync(guildId, $"role:{key}");
        return ulong.TryParse(value, out var id) ? id : null;
    }

    public Task SetRoleAsync(ulong guildId, string key, ulong roleId)
        => SetAsync(guildId, $"role:{key}", roleId.ToString());

    // ---------- level roles ----------

    public async Task<ulong?> GetLevelRoleAsync(ulong guildId, int threshold)
    {
        var value = await GetAsync(guildId, $"levelrole:{threshold}");
        return ulong.TryParse(value, out var id) ? id : null;
    }

    public Task SetLevelRoleAsync(ulong guildId, int threshold, ulong roleId)
        => SetAsync(guildId, $"levelrole:{threshold}", roleId.ToString());
}
