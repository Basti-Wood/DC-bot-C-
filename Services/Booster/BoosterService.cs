using DCBot.Core;
using DCBot.Core.Database;
using DCBot.Core.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCBot.Services.Booster;

/// <summary>Booster-Farb-Rollen + Rainbow-Persistenz (Port des booster-Moduls).</summary>
public sealed class BoosterService
{
    /// <summary>Wählbare Rollen-Keys — Reihenfolge = Rainbow-Rotation.</summary>
    public static readonly string[] SelectableRoleKeys =
    {
        "ROLE_UNSCHULDIGES_KIND",
        "ROLE_VERDAECHTIGES_KIND",
        "ROLE_SCHULDIGES_KIND",
        "ROLE_MIT_ENTFUEHRER",
        "ROLE_MEISTERENTFUEHRER",
        "ROLE_BEIFAHRER",
        "ROLE_VAN_UPGRADER",
    };

    /// <summary>Config-Key → Default-Anzeigename.</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultRoleLabels = new Dictionary<string, string>
    {
        ["ROLE_UNSCHULDIGES_KIND"] = "standard role",
        ["ROLE_VERDAECHTIGES_KIND"] = "level 20 role",
        ["ROLE_SCHULDIGES_KIND"] = "level 40 role",
        ["ROLE_MIT_ENTFUEHRER"] = "level 60 role",
        ["ROLE_MEISTERENTFUEHRER"] = "level 80 role",
        ["ROLE_BEIFAHRER"] = "level 100 role",
        ["ROLE_VAN_UPGRADER"] = "Booster_role",
    };

    public const string BoosterGateRoleKey = "ROLE_VAN_UPGRADER";

    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly BotConfig _config;
    private readonly GuildConfigService _guildConfig;

    public BoosterService(
        IDbContextFactory<BotDbContext> dbFactory,
        BotConfig config,
        GuildConfigService guildConfig)
    {
        _dbFactory = dbFactory;
        _config = config;
        _guildConfig = guildConfig;
    }

    public static string GetDefaultRoleLabel(string roleKey)
        => DefaultRoleLabels.TryGetValue(roleKey, out var label) ? label : roleKey;

    private static string RoleFlavorConfigKey(string roleKey)
        => $"flavor:booster:{roleKey}";

    public async Task<string> GetRoleLabelAsync(ulong guildId, string roleKey)
    {
        var custom = await _guildConfig.GetAsync(guildId, RoleFlavorConfigKey(roleKey));
        return string.IsNullOrWhiteSpace(custom) ? GetDefaultRoleLabel(roleKey) : custom;
    }

    public async Task<Dictionary<string, string>> GetRoleLabelsAsync(ulong guildId)
    {
        var map = new Dictionary<string, string>();
        foreach (var roleKey in SelectableRoleKeys)
            map[roleKey] = await GetRoleLabelAsync(guildId, roleKey);
        return map;
    }

    public Task SetRoleLabelAsync(ulong guildId, string roleKey, string label)
        => _guildConfig.SetAsync(guildId, RoleFlavorConfigKey(roleKey), label);

    public async Task<string?> ResolveRoleKeyByLabelAsync(ulong guildId, string userChoice)
    {
        if (string.IsNullOrWhiteSpace(userChoice))
            return null;

        var needle = userChoice.Trim();

        foreach (var roleKey in SelectableRoleKeys)
        {
            var label = await GetRoleLabelAsync(guildId, roleKey);

            if (string.Equals(needle, roleKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(needle, label, StringComparison.OrdinalIgnoreCase))
                return roleKey;
        }

        return null;
    }

    /// <summary>Config-Key → konfigurierte Rollen-ID (oder null).</summary>
    public async Task<Dictionary<string, ulong?>> GetRoleMapAsync(ulong guildId)
    {
        var map = new Dictionary<string, ulong?>();
        foreach (var roleKey in SelectableRoleKeys)
            map[roleKey] = await _guildConfig.GetRoleAsync(guildId, roleKey);
        return map;
    }

    public async Task SetRainbowActiveAsync(ulong guildId, ulong userId, bool active)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.RainbowUsers.FirstOrDefaultAsync(r =>
            r.BotId == _config.BotId && r.GuildId == guildId && r.UserId == userId);

        if (entry is null)
        {
            db.RainbowUsers.Add(new RainbowUser
            {
                BotId = _config.BotId,
                GuildId = guildId,
                UserId = userId,
                CurrentIndex = 0,
                Active = active,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            entry.Active = active;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Aktive Rainbow-User, deren letztes Update ≥ 30 Minuten her ist.</summary>
    public async Task<List<RainbowUser>> GetDueRainbowUsersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        return await db.RainbowUsers.AsNoTracking()
            .Where(r => r.BotId == _config.BotId && r.Active && r.UpdatedAt <= cutoff)
            .ToListAsync();
    }

    public async Task UpdateRainbowUserAsync(ulong guildId, ulong userId, int? newIndex)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.RainbowUsers.FirstOrDefaultAsync(r =>
            r.BotId == _config.BotId && r.GuildId == guildId && r.UserId == userId);
        if (entry is null) return;

        if (newIndex is { } idx) entry.CurrentIndex = idx;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
