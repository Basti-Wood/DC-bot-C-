using System.Reflection;
using DCBot.Commands;
using DCBot.Events;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DCBot.Core;

/// <summary>
/// The hosted service that ties everything together:
/// discovers commands + event listeners, connects the Discord client,
/// publishes guild slash commands on Ready and routes interactions
/// to the CommandHandler.
/// </summary>
public sealed class BotService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly CommandHandler _commands;
    private readonly EventManager _events;
    private readonly BotConfig _config;
    private readonly IServiceProvider _services;
    private readonly ILogger<BotService> _logger;

    public BotService(
        DiscordSocketClient client,
        CommandHandler commands,
        EventManager events,
        BotConfig config,
        IServiceProvider services,
        ILogger<BotService> logger)
    {
        _client = client;
        _commands = commands;
        _events = events;
        _config = config;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Discover all slash commands (Commands/<Name>/<Name>Command.cs)
        _commands.DiscoverCommands(_services);

        // 2. Discover all event listeners (Events/<Name>/...) and register them
        DiscoverEventListeners();

        // 3. Wire core handlers
        _client.Log += msg =>
        {
            _logger.Log(MapLevel(msg.Severity), "{Source}: {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        };

        _client.Ready += OnReadyAsync;

        // Publish commands automatically when the bot joins a new guild
        _client.JoinedGuild += guild =>
        {
            _ = Task.Run(async () =>
            {
                try { await _commands.PublishToGuildAsync(guild); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish commands to new guild {Guild}", guild.Id);
                }
            }, stoppingToken);
            return Task.CompletedTask;
        };

        _client.SlashCommandExecuted += interaction =>
        {
            _ = Task.Run(() => _commands.HandleAsync(interaction), stoppingToken);
            return Task.CompletedTask;
        };

        // 4. Attach module event handlers to the client
        _events.Attach(_client);

        // 5. Connect
        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();

        _logger.LogInformation("Bot [{BotId}] starting ...", _config.BotId);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }

        await _client.SetStatusAsync(UserStatus.Invisible);
        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    /// <summary>
    /// Find all IEventListener implementations, create them via DI and
    /// let them subscribe to the EventManager.
    /// </summary>
    private void DiscoverEventListeners()
    {
        var types = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IEventListener).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false })
            .OrderBy(t => t.Name);

        foreach (var type in types)
        {
            try
            {
                var listener = (IEventListener)ActivatorUtilities.CreateInstance(_services, type);
                listener.Register(_events);
                _logger.LogInformation("Loaded event listener: {Name}", listener.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load event listener {Type}", type.FullName);
            }
        }
    }

    private async Task OnReadyAsync()
    {
        _logger.LogInformation("Logged in as {User} ({Id})",
            _client.CurrentUser.Username, _client.CurrentUser.Id);

        await _client.SetActivityAsync(new Game("tuubaa :3", ActivityType.Watching));
        await _client.SetStatusAsync(UserStatus.Online);

        // Publish commands: to the configured guild, or to every guild the bot is in
        if (!string.IsNullOrWhiteSpace(_config.GuildId)
            && ulong.TryParse(_config.GuildId, out var guildId)
            && _client.GetGuild(guildId) is { } guild)
        {
            await _commands.PublishToGuildAsync(guild);
        }
        else
        {
            foreach (var g in _client.Guilds)
            {
                try { await _commands.PublishToGuildAsync(g); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish commands to guild {Guild}", g.Id);
                }
            }
        }
    }

    private static LogLevel MapLevel(LogSeverity severity) => severity switch
    {
        LogSeverity.Critical => LogLevel.Critical,
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Warning => LogLevel.Warning,
        LogSeverity.Info => LogLevel.Information,
        LogSeverity.Verbose => LogLevel.Debug,
        LogSeverity.Debug => LogLevel.Trace,
        _ => LogLevel.Information,
    };
}
