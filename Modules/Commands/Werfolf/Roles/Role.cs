using Discord.WebSocket;

namespace DCBot.Commands.Werwolf;

/// <summary>Welchem Lager eine Rolle angehört.</summary>
public enum Team
{
    Village,
    Werewolf,
}

/// <summary>
/// Basisklasse ("Job") für alle Werwolf-Rollen.
///
/// Jede Rolle ist eine eigene kleine Klasse, die von Role erbt. Die
/// Fähigkeit ("Ability") lebt IN der Rollenklasse:
///  - Zielauswahl:     GetNightTargets() + NightPrompt
///  - Einreichen:      OnNightSubmittedAsync()
///  - Nachtauflösung:  OnNightResolveAsync()  (Reihenfolge: NightPriority)
///  - Spielstart:      HasGameStartAction + OnGameStartAsync()/OnSetupSelectAsync()
///  - Tag:             OnDayStartAsync()/OnButtonAsync()/VoteWeight
///  - Tod:             OnDeathAsync()/OnDeathSelectAsync()
///
/// Der WerwolfGame-Engine bleiben nur die gemeinsamen Spielregeln
/// (Phasen, Abstimmung, Werwolf-Kill, Siegbedingungen).
/// Neue Rolle = 1 neue Datei + 1 Eintrag in RoleRegistry.
/// </summary>
public abstract class Role
{
    /// <summary>Eindeutiger Schlüssel (für Buttons/Selects, z.B. "wizard").</summary>
    public abstract string Key { get; }

    /// <summary>Anzeigename in DMs und Embeds.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Emoji für DMs/Nachrichten.</summary>
    public virtual string Emoji => "❓";

    /// <summary>Grundteam. Neutrale Rollen setzen IsNeutral; ihr Team wird bei Spielstart festgelegt.</summary>
    public virtual Team BaseTeam => Team.Village;

    public virtual bool IsWerewolf => false;
    public virtual bool IsNeutral => false;

    /// <summary>Rolle handelt nachts (bekommt eine DM mit Zielauswahl).</summary>
    public virtual bool HasNightAction => false;

    /// <summary>Rolle hat eine einmalige Aktion zu Spielbeginn (z.B. Amor).</summary>
    public virtual bool HasGameStartAction => false;

    /// <summary>Reihenfolge der Nachtauflösung (niedriger = früher).</summary>
    public virtual int NightPriority => 0;

    /// <summary>Stimmgewicht bei der Tag-Abstimmung.</summary>
    public virtual int VoteWeight => 1;

    /// <summary>Text im Nacht-Auswahlmenü.</summary>
    public virtual string NightPrompt => "Handle diese Nacht.";

    /// <summary>Beschreibung, die der Spieler per DM bekommt.</summary>
    public virtual string AbilityText => "Keine besondere Fähigkeit — finde die Werwölfe und stimme tagsüber mit ab!";

    /// <summary>Gültige Ziele der Nachtaktion.</summary>
    public virtual List<Player> GetNightTargets(WerwolfGame game, Player self) => new();

    /// <summary>Einmalige Aktion zu Spielbeginn (z.B. Amor verkuppelt).</summary>
    public virtual Task OnGameStartAsync(WerwolfGame game, Player self) => Task.CompletedTask;

    /// <summary>Select-Auswahl der Spielstart-Aktion (z.B. Amors zwei Partner).</summary>
    public virtual Task OnSetupSelectAsync(WerwolfGame game, Player self, SocketMessageComponent comp, string step, ulong value)
        => Task.CompletedTask;

    /// <summary>Nachtaktion wurde eingereicht (Bestätigung / Menü-Update).</summary>
    public virtual Task OnNightSubmittedAsync(WerwolfGame game, Player self, NightState night, SocketMessageComponent comp, ulong targetId)
        => Task.CompletedTask;

    /// <summary>Nachtauflösung dieser Rolle (in NightPriority-Reihenfolge).</summary>
    public virtual Task OnNightResolveAsync(WerwolfGame game, Player self, NightState night)
        => Task.CompletedTask;

    /// <summary>Tagesstart (z.B. Zwilling kann sich aufdecken).</summary>
    public virtual Task OnDayStartAsync(WerwolfGame game, Player self) => Task.CompletedTask;

    /// <summary>Rollen-Button am Tag (z.B. "Zwilling aufdecken").</summary>
    public virtual Task OnButtonAsync(WerwolfGame game, Player self, SocketMessageComponent comp, string action)
        => Task.CompletedTask;

    /// <summary>Select-Auswahl nach dem Tod (z.B. Rachsüchtige wählt ihr Opfer).</summary>
    public virtual Task OnDeathSelectAsync(WerwolfGame game, Player self, SocketMessageComponent comp, ulong value)
        => Task.CompletedTask;

    /// <summary>Tod der Rolle (z.B. Werwolf-Geist sieht weiter den Chat, Rachsucht).</summary>
    public virtual Task OnDeathAsync(WerwolfGame game, Player self) => Task.CompletedTask;
}
