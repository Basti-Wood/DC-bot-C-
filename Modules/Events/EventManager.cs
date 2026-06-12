using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events;

/// <summary>
/// THE event manager. Event listeners subscribe here (wie core.On(...) im
/// Go-Bot). Handlers laufen auf dem Thread-Pool und blockieren nie das Gateway.
/// </summary>
public sealed class EventManager
{
    private readonly List<Func<SocketMessage, Task>> _messageReceived = new();
    private readonly List<Func<SocketGuildUser, Task>> _userJoined = new();
    private readonly List<Func<SocketGuild, SocketUser, Task>> _userLeft = new();
    private readonly List<Func<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction, Task>> _reactionAdded = new();
    private readonly List<Func<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction, Task>> _reactionRemoved = new();
    private readonly List<Func<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task>> _messageDeleted = new();
    private readonly List<Func<SocketUser, SocketVoiceState, SocketVoiceState, Task>> _voiceStateUpdated = new();
    private readonly List<Func<SocketModal, Task>> _modalSubmitted = new();
    private readonly List<Func<SocketMessageComponent, Task>> _buttonExecuted = new();
    private readonly List<Func<Task>> _ready = new();

    private readonly ILogger<EventManager> _logger;
    private bool _attached;

    public EventManager(ILogger<EventManager> logger)
    {
        _logger = logger;
    }

    // ---- subscription API for event listeners ----
    public void OnMessageReceived(Func<SocketMessage, Task> handler) => _messageReceived.Add(handler);
    public void OnUserJoined(Func<SocketGuildUser, Task> handler) => _userJoined.Add(handler);
    public void OnUserLeft(Func<SocketGuild, SocketUser, Task> handler) => _userLeft.Add(handler);
    public void OnReactionAdded(Func<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction, Task> handler) => _reactionAdded.Add(handler);
    public void OnReactionRemoved(Func<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction, Task> handler) => _reactionRemoved.Add(handler);
    public void OnMessageDeleted(Func<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task> handler) => _messageDeleted.Add(handler);
    public void OnVoiceStateUpdated(Func<SocketUser, SocketVoiceState, SocketVoiceState, Task> handler) => _voiceStateUpdated.Add(handler);
    public void OnModalSubmitted(Func<SocketModal, Task> handler) => _modalSubmitted.Add(handler);
    public void OnButtonExecuted(Func<SocketMessageComponent, Task> handler) => _buttonExecuted.Add(handler);
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
        client.ReactionRemoved += (msg, ch, reaction) => Dispatch(_reactionRemoved, h => h(msg, ch, reaction));
        client.MessageDeleted += (msg, ch) => Dispatch(_messageDeleted, h => h(msg, ch));
        client.UserVoiceStateUpdated += (user, before, after) => Dispatch(_voiceStateUpdated, h => h(user, before, after));
        client.ModalSubmitted += modal => Dispatch(_modalSubmitted, h => h(modal));
        client.ButtonExecuted += component => Dispatch(_buttonExecuted, h => h(component));
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
