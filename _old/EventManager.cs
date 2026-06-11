using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Core;

/// <summary>
/// Central event manager. Modules subscribe their handlers here
/// (like core.On(...) in the Go bot). The manager attaches itself to the
/// Discord client once and fans every gateway event out to all subscribers.
/// Handlers run on the thread pool so they never block the gateway.
/// </summary>
public sealed class EventManager
{
    private readonly List<Func<SocketMessage, Task>> _messageReceived = new();
    private readonly List<Func<SocketGuildUser, Task>> _userJoined = new();
    private readonly List<Func<SocketGuild, SocketUser, Task>> _userLeft = new();
    private readonly List<Func<Discord.Cacheable<Discord.IUserMessage, ulong>, Discord.Cacheable<Discord.IMessageChannel, ulong>, SocketReaction, Task>> _reactionAdded = new();
    private readonly List<Func<SocketUser, SocketVoiceState, SocketVoiceState, Task>> _voiceStateUpdated = new();
    private readonly List<Func<Task>> _ready = new();

    private readonly ILogger<EventManager> _logger;
    private bool _attached;

    public EventManager(ILogger<EventManager> logger)
    {
        _logger = logger;
    }

    // ---- subscription API for modules ----
    public void OnMessageReceived(Func<SocketMessage, Task> handler) => _messageReceived.Add(handler);
    public void OnUserJoined(Func<SocketGuildUser, Task> handler) => _userJoined.Add(handler);
    public void OnUserLeft(Func<SocketGuild, SocketUser, Task> handler) => _userLeft.Add(handler);
    public void OnReactionAdded(Func<Discord.Cacheable<Discord.IUserMessage, ulong>, Discord.Cacheable<Discord.IMessageChannel, ulong>, SocketReaction, Task> handler) => _reactionAdded.Add(handler);
    public void OnVoiceStateUpdated(Func<SocketUser, SocketVoiceState, SocketVoiceState, Task> handler) => _voiceStateUpdated.Add(handler);
    public void OnReady(Func<Task> handler) => _ready.Add(handler);

    /// <summary>Wire all subscribed handlers to the Discord client. Called once at startup.</summary>
    public void Attach(DiscordSocketClient client)
    {
        if (_attached) return;
        _attached = true;

        client.MessageReceived += msg => Dispatch(_messageReceived, h => h(msg));
        client.UserJoined += user => Dispatch(_userJoined, h => h(user));
        client.UserLeft += (guild, user) => Dispatch(_userLeft, h => h(guild, user));
        client.ReactionAdded += (msg, ch, reaction) => Dispatch(_reactionAdded, h => h(msg, ch, reaction));
        client.UserVoiceStateUpdated += (user, before, after) => Dispatch(_voiceStateUpdated, h => h(user, before, after));
        client.Ready += () => Dispatch(_ready, h => h());
    }

    private Task Dispatch<T>(List<T> handlers, Func<T, Task> invoke)
    {
        foreach (var handler in handlers)
        {
            var h = handler;
            _ = Task.Run(async () =>
            {
                try
                {
                    await invoke(h);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Event handler threw an exception");
                }
            });
        }
        return Task.CompletedTask;
    }
}
