using Discord;
using Discord.WebSocket;

namespace DCBot.Commands;

/// <summary>
/// Every slash command implements this interface and lives in its OWN folder
/// under Commands/ (e.g. Commands/Ping/PingCommand.cs).
///
/// Commands are discovered automatically via reflection by the
/// CommandHandler — you never have to register them anywhere manually.
///
/// To add a new command:
///   1. Create a folder:  Commands/MyCommand/
///   2. Create a class:   MyCommandCommand : ICommand
///   3. Rebuild — done. The CommandHandler finds, publishes and routes it.
/// </summary>
public interface ICommand
{
    /// <summary>Slash command name (lowercase, no spaces).</summary>
    string Name { get; }

    string Description { get; }

    /// <summary>Cooldown per user in seconds. 0 = none.</summary>
    int Cooldown => 0;

    /// <summary>Everyone may use this command.</summary>
    bool AllowEveryone => false;

    /// <summary>Members with Administrator permission may use this command.</summary>
    bool AllowAdmin => false;

    /// <summary>Command options / subcommands (empty by default).</summary>
    List<SlashCommandOptionBuilder> BuildOptions() => new();

    /// <summary>The command logic.</summary>
    Task ExecuteAsync(SocketSlashCommand command);
}
