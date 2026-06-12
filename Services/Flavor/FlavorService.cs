using DCBot.Core;

namespace DCBot.Services.Flavor;

/// <summary>Guild-spezifische Flavor-Texte (z. B. Counter-Channel-Format).</summary>
public sealed class FlavorService
{
    public const string CounterTemplateConfigKey = "flavor:counter:template";
    public const string DefaultCounterTemplate = "「👥」Kinder✩{count}";

    private readonly GuildConfigService _guildConfig;

    public FlavorService(GuildConfigService guildConfig)
    {
        _guildConfig = guildConfig;
    }

    public async Task<string> GetCounterTemplateAsync(ulong guildId)
    {
        var value = await _guildConfig.GetAsync(guildId, CounterTemplateConfigKey);
        return string.IsNullOrWhiteSpace(value) ? DefaultCounterTemplate : value;
    }

    public Task SetCounterTemplateAsync(ulong guildId, string template)
        => _guildConfig.SetAsync(guildId, CounterTemplateConfigKey, template);

    public static string RenderCounterName(string template, int count)
    {
        var safeTemplate = string.IsNullOrWhiteSpace(template)
            ? DefaultCounterTemplate
            : template.Trim();

        var rendered = safeTemplate.Contains("{count}", StringComparison.OrdinalIgnoreCase)
            ? safeTemplate.Replace("{count}", count.ToString(), StringComparison.OrdinalIgnoreCase)
            : $"{safeTemplate}{count}";

        if (rendered.Length > 100)
            rendered = rendered[..100];

        return rendered;
    }
}
