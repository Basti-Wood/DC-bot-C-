using Discord;
using Discord.WebSocket;

namespace DCBot.Core;

/// <summary>
/// A slash command definition. Mirrors the Command struct from the Go bot:
/// name, description, options, cooldown, permission flags and a handler.
/// Modules create these and pass them to CommandManager.Register().
/// </summary>
public sealed class SlashCommand
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>Slash command options (parameters / subcommands).</summary>
    public List<SlashCommandOptionBuilder> Options { get; init; } = new();

    /// <summary>Cooldown in seconds per user. 0 = no cooldown.</summary>
    public int Cooldown { get; init; }

    /// <summary>Anyone may use the command.</summary>
    public bool AllowEveryone { get; init; }

    /// <summary>Members with the Administrator permission may use the command.</summary>
    public bool AllowAdmin { get; init; }

    /// <summary>The actual command logic.</summary>
    public required Func<SocketSlashCommand, Task> Handler { get; init; }
}
