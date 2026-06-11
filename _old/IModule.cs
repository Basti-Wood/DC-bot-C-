namespace DCBot.Core;

/// <summary>
/// Every module implements this interface. Modules are discovered
/// automatically via reflection (see ModuleLoader) — just like the Go bot's
/// blank imports + init() pattern, but without having to touch Program.cs.
///
/// To add a new module:
///   1. Create a folder under Modules/ (e.g. Modules/MyThing/)
///   2. Add a class implementing IModule
///   3. Register commands and events in Register()
/// Done — it gets picked up automatically at startup.
/// </summary>
public interface IModule
{
    string Name { get; }

    /// <summary>
    /// Register all commands and event handlers of this module.
    /// Called once at startup, before the client connects.
    /// </summary>
    void Register(CommandManager commands, EventManager events);
}
