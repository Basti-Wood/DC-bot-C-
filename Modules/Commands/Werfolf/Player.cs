using Discord.WebSocket;

namespace DCBot.Commands.Werwolf;

/// <summary>
/// Gemeinsamer Spielzustand eines Spielers (Spielregeln).
/// Rollen-spezifischer Zustand (z.B. geschützte Spieler, Rache genutzt,
/// Zwilling aufgedeckt) lebt in der jeweiligen Rollenklasse.
/// </summary>
public sealed class Player
{
    public required SocketGuildUser User { get; init; }
    public required Role Role { get; set; }

    /// <summary>Effektives Team für neutrale Rollen (wird bei Spielstart zugewiesen).</summary>
    public Team NeutralTeam { get; set; } = Team.Village;

    public bool Alive { get; set; } = true;

    /// <summary>Für den aktuellen Tag stummgeschaltet (darf nicht abstimmen).</summary>
    public bool MutedForRound { get; set; }

    public Player? Lover { get; set; }
}
