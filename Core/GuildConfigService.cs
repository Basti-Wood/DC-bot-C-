using DCBot.Core.Database;
using DCBot.Core.Database.Entities;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace DCBot.Core;

/// <summary>
/// Shared access to per-guild key/value config. All scoped to this BotId.
/// Keys:  channel:&lt;which&gt;   role:&lt;key&gt;   (Level-Rollen: role:Role_level_&lt;N&gt;)
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

    public const string LevelRolePrefix = "Role_level_";

    public static string LevelRoleKey(int level) => $"{LevelRolePrefix}{level}";

    public Task SetLevelRoleAsync(ulong guildId, int level, ulong roleId)
        => SetRoleAsync(guildId, LevelRoleKey(level), roleId);

    /// <summary>Alle konfigurierten Level-Rollen der Guild, aufsteigend nach Level.</summary>
    public async Task<List<(int Level, ulong RoleId)>> GetLevelRolesAsync(ulong guildId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var prefix = $"role:{LevelRolePrefix}";
        var entries = await db.GuildConfigs.AsNoTracking()
            .Where(c => c.BotId == _config.BotId && c.GuildId == guildId && c.Key.StartsWith(prefix))
            .ToListAsync();

        var result = new List<(int Level, ulong RoleId)>();
        foreach (var e in entries)
            if (int.TryParse(e.Key[prefix.Length..], out var lvl) && ulong.TryParse(e.Value, out var id))
                result.Add((lvl, id));
        return result.OrderBy(r => r.Level).ToList();
    }

    /// <summary>Gibt dem User die höchste erreichte Level-Rolle und entfernt alle anderen Level-Rollen.</summary>
    public async Task ApplyLevelRoleAsync(SocketGuildUser user, int level)
    {
        var roles = await GetLevelRolesAsync(user.Guild.Id);
        var target = roles.LastOrDefault(r => r.Level <= level).RoleId;

        var remove = roles.Select(r => r.RoleId)
            .Where(id => id != target && user.Roles.Any(r => r.Id == id))
            .Distinct().ToList();
        if (remove.Count > 0) await user.RemoveRolesAsync(remove);
        if (target != 0 && user.Roles.All(r => r.Id != target)) await user.AddRoleAsync(target);
    }

    // ---------- Migration alter Keys ----------

    private static readonly (string Old, string New)[] RenamedRoleKeys =
    {
        ("ROLE_UNSCHULDIGES_KIND", "Role_standard"),
        ("ROLE_VERDAECHTIGES_KIND", "Role_level_20"),
        ("ROLE_SCHULDIGES_KIND", "Role_level_40"),
        ("ROLE_MIT_ENTFUEHRER", "Role_level_60"),
        ("ROLE_MEISTERENTFUEHRER", "Role_level_80"),
        ("ROLE_BEIFAHRER", "Role_level_100"),
        ("ROLE_VAN_UPGRADER", "Role_booster"),
    };

    /// <summary>Benennt alte Config-Keys einmalig in die neuen, generischen Keys um.</summary>
    public async Task MigrateKeysAsync()
    {
        var map = new Dictionary<string, string>();
        foreach (var (o, n) in RenamedRoleKeys)
        {
            map[$"role:{o}"] = $"role:{n}";
            map[$"flavor:booster:{o}"] = $"flavor:booster:{n}";
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var all = await db.GuildConfigs.Where(c => c.BotId == _config.BotId).ToListAsync();
        var existing = all.Select(c => (c.GuildId, c.Key)).ToHashSet();

        foreach (var entry in all)
        {
            var newKey = map.GetValueOrDefault(entry.Key)
                ?? (entry.Key.StartsWith("levelrole:") ? $"role:{LevelRolePrefix}{entry.Key["levelrole:".Length..]}" : null);
            if (newKey is null) continue;

            if (existing.Add((entry.GuildId, newKey))) entry.Key = newKey;
            else db.GuildConfigs.Remove(entry);
        }

        await db.SaveChangesAsync();
    }
}
