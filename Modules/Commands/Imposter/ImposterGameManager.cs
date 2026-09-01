using System.Collections.Concurrent;

namespace DCBot.Commands.Imposter;

/// <summary>Hält alle aktiven Imposter-Spiele — mehrere pro Guild möglich.</summary>
public sealed class ImposterGameManager
{
    private readonly ConcurrentDictionary<string, ImposterGame> _games = new();
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>> _byGuild = new();

    public ImposterGame? GetByGameId(string gameId)
        => _games.TryGetValue(gameId, out var game) ? game : null;

    /// <summary>Alle aktiven Spiele einer Guild (leer, wenn keins läuft).</summary>
    public IReadOnlyList<ImposterGame> GetByGuild(ulong guildId)
    {
        if (!_byGuild.TryGetValue(guildId, out var ids)) return Array.Empty<ImposterGame>();
        return ids.Keys
            .Select(id => _games.TryGetValue(id, out var game) ? game : null)
            .OfType<ImposterGame>()
            .ToList();
    }

    public bool TryRegister(ImposterGame game)
    {
        if (!_games.TryAdd(game.GameId, game)) return false;
        _byGuild.GetOrAdd(game.Guild.Id, _ => new ConcurrentDictionary<string, byte>())
            .TryAdd(game.GameId, 0);
        return true;
    }

    public void Remove(ImposterGame game)
    {
        _games.TryRemove(game.GameId, out _);
        if (_byGuild.TryGetValue(game.Guild.Id, out var ids))
        {
            ids.TryRemove(game.GameId, out _);
            if (ids.IsEmpty) _byGuild.TryRemove(game.Guild.Id, out _);
        }
    }
}
