namespace DCBot.Events;

/// <summary>
/// Every event listener implements this interface and lives in its own
/// folder under Events/ (e.g. Events/LevelXp/MessageXpEvent.cs).
/// Listeners are discovered via reflection at startup — no manual wiring.
///
/// To add a new event listener:
///   1. Create a folder:  Events/MyEvent/
///   2. Create a class:   MyEvent : IEventListener
///   3. Subscribe in Register(), e.g. events.OnMessageReceived(...)
/// </summary>
public interface IEventListener
{
    string Name { get; }

    void Register(EventManager events);
}
