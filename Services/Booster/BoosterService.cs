using DCBot.Core;

namespace DCBot.Services.Booster;

/// <summary>Booster-Farb-Rollen (Port des booster-Moduls).</summary>
public sealed class BoosterService
{
    public const string StandardRoleKey = "Role_standard";
    public const string BoosterGateRoleKey = "Role_booster";

    /// <summary>Wählbare Rollen-Keys — Standard, alle Level-Rollen (aufsteigend), Booster.</summary>
    public async Task<List<string>> GetSelectableRoleKeysAsync(ulong guildId)
    {
        var keys = new List<string> { StandardRoleKey };
        keys.AddRange((await _guildConfig.GetLevelRolesAsync(guildId))
            .Select(r => GuildConfigService.LevelRoleKey(r.Level)));
        keys.Add(BoosterGateRoleKey);
        return keys;
    }

    private readonly GuildConfigService _guildConfig;

    public BoosterService(GuildConfigService guildConfig)
    {
        _guildConfig = guildConfig;
    }

    public static string GetDefaultRoleLabel(string roleKey) => roleKey switch
    {
        StandardRoleKey => "Standard",
        BoosterGateRoleKey => "Booster",
        _ when roleKey.StartsWith(GuildConfigService.LevelRolePrefix)
            => $"Level {roleKey[GuildConfigService.LevelRolePrefix.Length..]}",
        _ => roleKey,
    };

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
        foreach (var roleKey in await GetSelectableRoleKeysAsync(guildId))
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

        foreach (var roleKey in await GetSelectableRoleKeysAsync(guildId))
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
        foreach (var roleKey in await GetSelectableRoleKeysAsync(guildId))
            map[roleKey] = await _guildConfig.GetRoleAsync(guildId, roleKey);
        return map;
    }
}
