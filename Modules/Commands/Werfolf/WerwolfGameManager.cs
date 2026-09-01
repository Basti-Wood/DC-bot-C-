using System.Collections.Concurrent;

namespace DCBot.Commands.Werwolf;

/// <summary>Hält alle aktiven Werwolf-Spiele — mehrere pro Guild möglich.</summary>
public sealed class WerwolfGameManager
{
    private readonly ConcurrentDictionary<string, WerwolfGame> _games = new();
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>> _byGuild = new();

    public WerwolfGame? GetByGameId(string gameId)
        => _games.TryGetValue(gameId, out var game) ? game : null;

    /// <summary>Alle aktiven Spiele einer Guild (leer, wenn keins läuft).</summary>
    public IReadOnlyList<WerwolfGame> GetByGuild(ulong guildId)
    {
        if (!_byGuild.TryGetValue(guildId, out var ids)) return Array.Empty<WerwolfGame>();
        return ids.Keys
            .Select(id => _games.TryGetValue(id, out var game) ? game : null)
            .OfType<WerwolfGame>()
            .ToList();
    }

    public bool TryRegister(WerwolfGame game)
    {
        if (!_games.TryAdd(game.GameId, game)) return false;
        _byGuild.GetOrAdd(game.Guild.Id, _ => new ConcurrentDictionary<string, byte>())
            .TryAdd(game.GameId, 0);
        return true;
    }

    public void Remove(WerwolfGame game)
    {
        _games.TryRemove(game.GameId, out _);
        if (_byGuild.TryGetValue(game.Guild.Id, out var ids))
        {
            ids.TryRemove(game.GameId, out _);
            if (ids.IsEmpty) _byGuild.TryRemove(game.Guild.Id, out _);
        }
    }
}
