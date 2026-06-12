namespace DCBot.Services.Misc;

/// <summary>Server-Regeln — geteilt von /rule und /setup rules.</summary>
public static class Rules
{
    public static readonly IReadOnlyDictionary<string, string> Texts = new Dictionary<string, string>
    {
        ["1"] = "**§1** Begegne allen Nutzern jederzeit freundlich und respektvoll.",
        ["2"] = "**§2** Beachte die Nutzungsbedingungen (Terms of Service) von Discord.",
        ["3"] = "**§3** Werbung für eigene oder fremde Inhalte ist nicht erlaubt.",
        ["4"] = "**§4** Diskussionen über sensible Themen wie Politik oder Religion sind untersagt.",
        ["5"] = "**§5** Beleidigungen, Provokationen sowie rassistische, sexistische oder radikale Aussagen werden nicht toleriert.",
        ["6"] = "**§6** Namen, Profilbilder und Status dürfen keine Beleidigungen, Provokationen oder extremen Aussagen enthalten. Bei einem Hinweis durch das Team sind diese unverzüglich zu ändern.",
        ["7"] = "**§7** Das Vortäuschen einer fremden Identität ist verboten.",
        ["8"] = "**§8** Die Nutzung mehrerer Discord-Accounts ist untersagt.",
        ["9"] = "**§9** Störungen in Sprachkanälen durch laute Geräusche, Stimmverzerrer, Soundboards o. Ä. sind verboten.",
        ["10"] = "**§10** Das Teilen von NSFW-Inhalten oder ähnlichem ist strengstens untersagt.",
        ["11"] = "**§11** Der Support darf nicht missbraucht werden. Wendet euch nur bei ernsthaften Anliegen an das Team.",
        ["12"] = "**§12** Betteln oder Nachfragen nach Rängen ist nicht gestattet.",
        ["13"] = "**§13** Den Anweisungen des Teams ist Folge zu leisten. In Zweifelsfällen hat das Team Entscheidungsrecht, auch über das Regelwerk hinaus.",
        ["14"] = "**§14** Kein \"Backseat Arting\": Wenn jemand eine Zeichnung postet und nicht explizit um Feedback bittet, ist jegliche Form von Kritik zu unterlassen (z. B. „Ich hätte das anders gemacht.).",
        ["15"] = "**§15** Dating, Flirten oder unangemessenes Verhalten sind auf dem Server nicht gestattet.",
        ["16"] = "**§16** Bitte fragt **tuubaa** nicht, ob ich eure Freundschaftsanfragen annehmen euch in einem Video malen kann.",
    };

    /// <summary>Choice-Labels für den /rule Command (gekürzt wie im Go-Bot).</summary>
    public static readonly (string Name, string Value)[] Choices =
    {
        ("§1 Begegne allen Nutzern jederzeit freundlich und respektvoll.", "1"),
        ("§2 Beachte die Nutzungsbedingungen (Terms of Service) von Discord.", "2"),
        ("§3 Werbung für eigene oder fremde Inhalte ist nicht erlaubt.", "3"),
        ("§4 Diskussionen über sensible Themen wie Politik oder Religion sind untersagt.", "4"),
        ("§5 Beleidigungen, Provokationen sowie rassistische, sexistische oder radikale...", "5"),
        ("§6 Namen, Profilbilder und Status dürfen keine Beleidigungen, Provokationen...", "6"),
        ("§7 Das Vortäuschen einer fremden Identität ist verboten.", "7"),
        ("§8 Die Nutzung mehrerer Discord-Accounts ist untersagt.", "8"),
        ("§9 Störungen in Sprachkanälen durch laute Geräusche, Stimmverzerrer...", "9"),
        ("§10 Das Teilen von NSFW-Inhalten oder ähnlichem ist strengstens untersagt.", "10"),
        ("§11 Der Support darf nicht missbraucht werden. Wendet euch nur bei...", "11"),
        ("§12 Betteln oder Nachfragen nach Rängen ist nicht gestattet.", "12"),
        ("§13 Den Anweisungen des Teams ist Folge zu leisten. In Zweifelsfällen hat...", "13"),
        ("§14 Kein \"Backseat Arting\": Wenn jemand eine Zeichnung postet und nicht...", "14"),
        ("§15 Dating, Flirten oder unangemessenes Verhalten sind auf dem Server nicht...", "15"),
        ("§16 Bitte fragt tuubaa nicht, ob ich eure Freundschaftsanfragen...", "16"),
    };

    public static string DefaultRuleText()
        => string.Join("\n\n", Enumerable.Range(1, Texts.Count)
            .Select(i => Texts.GetValueOrDefault(i.ToString()))
            .Where(t => t is not null));
}
