using System.Text.Json;
using System.Text.RegularExpressions;

namespace DCBot.Services.Misc;

public sealed class TextAssetService
{
    private const int MaxChoiceLabelLength = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object _fileLock = new();
    private readonly string _textDirectory;

    public TextAssetService()
    {
        _textDirectory = Path.Combine(AppContext.BaseDirectory, "assets", "text");
        Directory.CreateDirectory(_textDirectory);
    }

    public RulesTextAsset GetRules()
        => Load("rules.json", CreateDefaultRules);

    public Task SaveRulesAsync(RulesTextAsset rules)
        => SaveAsync("rules.json", rules);

    public GoodnightTextAsset GetGoodnight()
        => Load("GN.json", CreateDefaultGoodnight);

    public Task SaveGoodnightAsync(GoodnightTextAsset goodnight)
        => SaveAsync("GN.json", goodnight);

    public WelcomeTextAsset GetWelcome()
        => Load("welcome.json", CreateDefaultWelcome);

    public Task SaveWelcomeAsync(WelcomeTextAsset welcome)
        => SaveAsync("welcome.json", welcome);

    public async Task<bool> UpdateRuleTextAsync(string ruleId, string text)
    {
        var rules = GetRules();
        var rule = rules.Rules.FirstOrDefault(entry => entry.Id == ruleId);
        if (rule is null)
            return false;

        rule.Text = text.Trim();
        await SaveRulesAsync(rules);
        return true;
    }

    public async Task<bool> UpdateRuleLabelAsync(string ruleId, string label)
    {
        var rules = GetRules();
        var rule = rules.Rules.FirstOrDefault(entry => entry.Id == ruleId);
        if (rule is null)
            return false;

        rule.Label = label.Trim();
        await SaveRulesAsync(rules);
        return true;
    }

    public async Task<bool> UpdateGoodnightMessageAsync(int index, string text)
    {
        var goodnight = GetGoodnight();
        if (index < 1 || index > goodnight.Messages.Count)
            return false;

        goodnight.Messages[index - 1] = text.Trim();
        await SaveGoodnightAsync(goodnight);
        return true;
    }

    public async Task AddGoodnightMessageAsync(string text)
    {
        var goodnight = GetGoodnight();
        goodnight.Messages.Add(text.Trim());
        await SaveGoodnightAsync(goodnight);
    }

    public async Task<bool> RemoveGoodnightMessageAsync(int index)
    {
        var goodnight = GetGoodnight();
        if (index < 1 || index > goodnight.Messages.Count)
            return false;

        goodnight.Messages.RemoveAt(index - 1);
        await SaveGoodnightAsync(goodnight);
        return true;
    }

    public async Task<bool> UpdateWelcomeFieldAsync(string field, string text)
    {
        var welcome = GetWelcome();
        switch (field)
        {
            case "underage_dm":
                welcome.UnderageDm = text.Trim();
                break;
            case "embed_title":
                welcome.EmbedTitle = text.Trim();
                break;
            case "embed_description_with_main":
                welcome.EmbedDescriptionWithMainChannel = text.Trim();
                break;
            case "embed_description_without_main":
                welcome.EmbedDescriptionWithoutMainChannel = text.Trim();
                break;
            case "embed_footer":
                welcome.EmbedFooter = text.Trim();
                break;
            case "main_announcement":
                welcome.MainAnnouncement = text.Trim();
                break;
            default:
                return false;
        }

        await SaveWelcomeAsync(welcome);
        return true;
    }

    public IReadOnlyList<(string Label, string Value)> GetRuleChoices()
        => GetRules().Rules
            .Select(rule => (ShortenChoiceLabel(rule.Label), rule.Id))
            .ToArray();

    public string BuildDefaultRuleText()
        => string.Join("\n\n", GetRules().Rules.Select(rule => rule.Text));

    public static string RenderTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        var rendered = template;
        foreach (var (key, value) in values)
            rendered = rendered.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        return rendered;
    }

    public static string Summarize(string text, int maxLength = 100)
    {
        var singleLine = Regex.Replace(text, "\\s+", " ").Trim();
        if (singleLine.Length <= maxLength)
            return singleLine;

        return singleLine[..(maxLength - 3)] + "...";
    }

    private T Load<T>(string fileName, Func<T> fallbackFactory) where T : class
    {
        var path = Path.Combine(_textDirectory, fileName);

        lock (_fileLock)
        {
            if (!File.Exists(path))
            {
                var fallback = fallbackFactory();
                File.WriteAllText(path, JsonSerializer.Serialize(fallback, JsonOptions));
                return fallback;
            }

            var json = File.ReadAllText(path);
            var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
            if (value is not null)
                return value;

            var replacement = fallbackFactory();
            File.WriteAllText(path, JsonSerializer.Serialize(replacement, JsonOptions));
            return replacement;
        }
    }

    private Task SaveAsync<T>(string fileName, T value)
    {
        var path = Path.Combine(_textDirectory, fileName);
        var json = JsonSerializer.Serialize(value, JsonOptions);

        lock (_fileLock)
            File.WriteAllText(path, json);

        return Task.CompletedTask;
    }

    private static RulesTextAsset CreateDefaultRules()
        => new()
        {
            Rules =
            [
                new RuleEntry { Id = "1", Label = "§1 Begegne allen Nutzern jederzeit freundlich und respektvoll.", Text = "**§1** Bei unklarheiten im regelwerk müssen die Mods ausnahmsweise darüber entscheiden." },
                new RuleEntry { Id = "2", Label = "§2 Keine Beleidigungen und/oder radikale Aussagen.", Text = "**§2** Keine Beleidigungen und oder Radikale aussagen" },
                new RuleEntry { Id = "3", Label = "§3 Keine rassistischen, sexistischen oder anderweitig diskriminierenden Inhalte.", Text = "**§3** Keine rassistischen, sexistischen oder anderweitig diskriminierenden Inhalte." },
                new RuleEntry { Id = "4", Label = "§4 Werbung jeglicher Form ist zu 99% untersagt. (Es kann Ausnahmen geben)", Text = "**§4** Werbung jeglicher form ist zu 99% untersagt. (es kann ausnahmen geben)" },
                new RuleEntry { Id = "5", Label = "§5 Das Stören von VC-Gesprächen kann zu einer Suspension von diesen führen, bis zum Server-Bann.", Text = "**§5** das stören von VC gesprächen kann zu einer suspension von diesen führen, bis zum server bann." },
                new RuleEntry { Id = "6", Label = "§6 Material von <#1191008771955232919> dürft ihr überall benutzen, jedoch müsst ihr den Personen dort Credit geben.", Text = "**§6** Material von <#1191008771955232919> dürft ihr überall benutzen, jedoch müsst ihr den personen dort Credit geben" },
                new RuleEntry { Id = "7", Label = "§7 Haltet euch an Discords TOS.", Text = "**§7** Haltet euch an discords TOS" },
                new RuleEntry { Id = "8", Label = "§8 NSFW-Content ist nicht gestattet.", Text = "**§8** NSFW content ist nicht gestattet" },
                new RuleEntry { Id = "9", Label = "§9 Haltet euch wenn möglich an die Geneva Suggestion.", Text = "**§9** Haltet euch wenn möglich an die Geneva Suggestion" },
                new RuleEntry { Id = "10", Label = "§10 Keine unnötigen Pings.", Text = "**§10** Keine unnötigen pings" },
                new RuleEntry { Id = "11", Label = "§11 Seid nett zueinander, auch wenn ihr anderer Meinung seid.", Text = "**§11** Seid nett zueinander, auch wenn ihr anderer Meinung seid" },
                new RuleEntry { Id = "12", Label = "§12 Bitte nicht nach FA fragen, die Antwort ist meistens Nein.", Text = "**§12** Bitte nicht nach FA fragen, die Antwort ist meistens Nein" },
            ]
        };

    private static GoodnightTextAsset CreateDefaultGoodnight()
        => new()
        {
            Messages =
            [
                "Gute nacht liebe Kinnas <3",
                "Schlaft gut ihr kleinen Hosenschisser :3",
                "Gute Nacht, Gefangene des fleischkörpers!",
                "schlafet schön. möge euer skellet noch nciht frei brechen. oder so."
            ]
        };

    private static WelcomeTextAsset CreateDefaultWelcome()
        => new()
        {
            UnderageDm = "Dein Discord Account ist leider noch nicht alt genug, um dem Server beizutreten. Bitte versuche es erneut, wenn dein Account mindestens 15 Tage alt ist.",
            EmbedTitle = "Willkommen im goldenen Van, {displayName}! 💜",
            EmbedDescriptionWithMainChannel = "Schau doch mal in {mainChannelMention} vorbei!",
            EmbedDescriptionWithoutMainChannel = "Viel Spaß auf dem Server!",
            EmbedFooter = "Mitglied #{memberCount}",
            MainAnnouncement = "Ein neuer gefangener im **goldenen Van**! Heißt {userMention} willkommen <:HeyTuba:1137369596496707624>"
        };

    private static string ShortenChoiceLabel(string label)
        => label.Length <= MaxChoiceLabelLength ? label : label[..(MaxChoiceLabelLength - 3)] + "...";
}

public sealed class RulesTextAsset
{
    public List<RuleEntry> Rules { get; init; } = [];
}

public sealed class RuleEntry
{
    public string Id { get; init; } = "";
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class GoodnightTextAsset
{
    public List<string> Messages { get; init; } = [];
}

public sealed class WelcomeTextAsset
{
    public string UnderageDm { get; set; } = "";
    public string EmbedTitle { get; set; } = "";
    public string EmbedDescriptionWithMainChannel { get; set; } = "";
    public string EmbedDescriptionWithoutMainChannel { get; set; } = "";
    public string EmbedFooter { get; set; } = "";
    public string MainAnnouncement { get; set; } = "";
}