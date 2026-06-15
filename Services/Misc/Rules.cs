namespace DCBot.Services.Misc;

/// <summary>Server-Regeln — geteilt von /rule und /setup rules.</summary>
public static class Rules
{
    public static readonly IReadOnlyDictionary<string, string> Texts = new Dictionary<string, string>
    {
        ["1"] = "**§1** Bei unklarheiten im regelwerk müssen die Mods ausnahmsweise darüber entscheiden.",
		["2"] = "**§2** Keine Beleidigungen und oder Radikale aussagen",
		["3"] = "**§3** Keine rassistischen, sexistischen oder anderweitig diskriminierenden Inhalte.",
		["4"] = "**§4** Werbung jeglicher form ist zu 99% untersagt. (es kann ausnahmen geben)",
		["5"] = "**§5** das stören von VC gesprächen kann zu einer suspension von diesen führen, bis zum server bann.",
		["6"] = "**§6** Material von <#1191008771955232919> dürft ihr überall benutzen, jedoch müsst ihr den personen dort Credit geben",
		["7"] = "**§7** Haltet euch an discords TOS",
		["8"] = "**§8** NSFW content ist nicht gestattet",
		["9"] = "**§9** Haltet euch wenn möglich an die Geneva Suggestion",
		["10"] = "**§10** Keine unnötigen pings",
		["11"] = "**§11** Seid nett zueinander, auch wenn ihr anderer Meinung seid",
		["12"] = "**§12** Bitte nicht nach FA fragen, die Antwort ist meistens Nein",

    };

    /// <summary>Choice-Labels für den /rule Command (gekürzt wie im Go-Bot).</summary>
    public static readonly (string Name, string Value)[] Choices =
    {
        ("§1 Begegne allen Nutzern jederzeit freundlich und respektvoll.", "1"),
		("§2 Keine Beleidigungen und/oder radikale Aussagen.", "2"),
		("§3 Keine rassistischen, sexistischen oder anderweitig diskriminierenden Inhalte.", "3"),
		("§4 Werbung jeglicher Form ist zu 99% untersagt. (Es kann Ausnahmen geben)", "4"),
		("§5 Das Stören von VC-Gesprächen kann zu einer Suspension von diesen führen, bis zum Server-Bann.", "5"),
		("§6 Material von <#1191008771955232919> dürft ihr überall benutzen, jedoch müsst ihr den Personen dort Credit geben.", "6"),
		("§7 Haltet euch an Discords TOS.", "7"),
		("§8 NSFW-Content ist nicht gestattet.", "8"),
		("§9 Haltet euch wenn möglich an die Geneva Suggestion.", "9"),
		("§10 Keine unnötigen Pings.", "10"),
		("§11 Seid nett zueinander, auch wenn ihr anderer Meinung seid.", "11"),
		("§12 Bitte nicht nach FA fragen, die Antwort ist meistens Nein.", "12"),
    };

    public static string DefaultRuleText()
        => string.Join("\n\n", Enumerable.Range(1, Texts.Count)
            .Select(i => Texts.GetValueOrDefault(i.ToString()))
            .Where(t => t is not null));
}
