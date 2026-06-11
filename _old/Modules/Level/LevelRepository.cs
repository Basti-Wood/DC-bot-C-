using DCBot.Core;
using DCBot.Core.Database;
using DCBot.Core.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCBot.Modules.Level;

/// <summary>
/// Database access for the level module. Everything is scoped to
/// (BotId, GuildId) so multiple bots and guilds never mix data.
/// </summary>
public sealed class LevelRepository
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly BotConfig _config;

    public LevelRepository(IDbContextFactory<BotDbContext> dbFactory, BotConfig config)
    {
        _dbFactory = dbFactory;
        _config = config;
    }

    public async Task<long> GetXpAsync(ulong guildId, ulong userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.UserLevels.AsNoTracking()
            .FirstOrDefaultAsync(u => u.BotId == _config.BotId && u.GuildId == guildId && u.UserId == userId);
        return entry?.Xp ?? 0;
    }

    public async Task SetXpAsync(ulong guildId, ulong userId, long xp)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.UserLevels
            .FirstOrDefaultAsync(u => u.BotId == _config.BotId && u.GuildId == guildId && u.UserId == userId);

        if (entry is null)
        {
            db.UserLevels.Add(new UserLevel
            {
                BotId = _config.BotId,
                GuildId = guildId,
                UserId = userId,
                Xp = xp,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            entry.Xp = xp;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public async Task<long> AddXpAsync(ulong guildId, ulong userId, long amount)
    {
        var current = await GetXpAsync(guildId, userId);
        var updated = current + amount;
        await SetXpAsync(guildId, userId, updated);
        return updated;
    }

    /// <summary>Top users by XP for the leaderboard, paged.</summary>
    public async Task<List<UserLevel>> GetTopAsync(ulong guildId, int page, int pageSize)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.UserLevels.AsNoTracking()
            .Where(u => u.BotId == _config.BotId && u.GuildId == guildId)
            .OrderByDescending(u => u.Xp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>1-based rank of a user in their guild (by XP).</summary>
    public async Task<int> GetRankAsync(ulong guildId, ulong userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var xp = await GetXpAsync(guildId, userId);
        var higher = await db.UserLevels.AsNoTracking()
            .CountAsync(u => u.BotId == _config.BotId && u.GuildId == guildId && u.Xp > xp);
        return higher + 1;
    }

    public async Task<int> CountAsync(ulong guildId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.UserLevels.AsNoTracking()
            .CountAsync(u => u.BotId == _config.BotId && u.GuildId == guildId);
    }
}
