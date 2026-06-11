using System.Collections.Concurrent;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands;

/// <summary>
/// THE command handler. Lives in the root of the Commands/ folder.
///
/// - Discovers every ICommand in the assembly via reflection (each command
///   has its own folder under Commands/)
/// - Publishes them as guild slash commands (bulk overwrite)
/// - Dispatches incoming interactions to the right command
/// - Enforces cooldowns and permissions (AllowEveryone / AllowAdmin)
/// </summary>
public sealed class CommandHandler
{
    private readonly Dictionary<string, ICommand> _commands = new();
    private readonly ConcurrentDictionary<string, long> _cooldowns = new();
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(ILogger<CommandHandler> logger)
    {
        _logger = logger;
    }

    public IReadOnlyDictionary<string, ICommand> Commands => _commands;

    /// <summary>
    /// Find all ICommand implementations and instantiate them through DI
    /// (so commands can request services like LevelRepository, AiService,
    /// the Discord client, loggers, ... in their constructors).
    /// </summary>
    public void DiscoverCommands(IServiceProvider services)
    {
        var types = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(ICommand).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false })
            .OrderBy(t => t.Name);

        foreach (var type in types)
        {
            try
            {
                var cmd = (ICommand)ActivatorUtilities.CreateInstance(services, type);

                if (!_commands.TryAdd(cmd.Name, cmd))
                {
                    _logger.LogError("Duplicate command name '{Name}' ({Type}) — skipped",
                        cmd.Name, type.FullName);
                    continue;
                }

                _logger.LogInformation("Loaded command /{Name} ({Type})", cmd.Name, type.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load command {Type}", type.FullName);
            }
        }
    }

    /// <summary>Publish all commands to a guild (bulk overwrite, instant).</summary>
    public async Task PublishToGuildAsync(SocketGuild guild)
    {
        var props = _commands.Values
            .Select(c =>
            {
                var builder = new SlashCommandBuilder()
                    .WithName(c.Name)
                    .WithDescription(c.Description);
                foreach (var opt in c.BuildOptions())
                    builder.AddOption(opt);
                return (ApplicationCommandProperties)builder.Build();
            })
            .ToArray();

        await guild.BulkOverwriteApplicationCommandAsync(props);
        _logger.LogInformation("Published {Count} slash commands to guild {Guild} ({Id})",
            props.Length, guild.Name, guild.Id);
    }

    /// <summary>Dispatch an incoming slash command interaction.</summary>
    public async Task HandleAsync(SocketSlashCommand interaction)
    {
        if (!_commands.TryGetValue(interaction.CommandName, out var cmd))
        {
            await interaction.RespondAsync("Diesen Befehl gibt es nicht mehr.", ephemeral: true);
            return;
        }

        // --- permissions ---
        var guildUser = interaction.User as SocketGuildUser;
        var isAdmin = guildUser?.GuildPermissions.Administrator ?? false;

        if (!(cmd.AllowEveryone || (cmd.AllowAdmin && isAdmin)))
        {
            await interaction.RespondAsync("Du hast keine Berechtigung für diesen Befehl.", ephemeral: true);
            return;
        }

        // --- cooldown ---
        if (cmd.Cooldown > 0)
        {
            var key = $"{interaction.User.Id}:{cmd.Name}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (_cooldowns.TryGetValue(key, out var expires) && expires > now)
            {
                await interaction.RespondAsync(
                    $"Du hast noch {expires - now} Sekunden Cooldown.", ephemeral: true);
                return;
            }
            _cooldowns[key] = now + cmd.Cooldown;
        }

        // --- execute ---
        try
        {
            await cmd.ExecuteAsync(interaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command /{Name} failed", cmd.Name);
            try
            {
                if (!interaction.HasResponded)
                    await interaction.RespondAsync("Ein interner Fehler ist aufgetreten.", ephemeral: true);
                else
                    await interaction.FollowupAsync("Ein interner Fehler ist aufgetreten.", ephemeral: true);
            }
            catch { /* interaction may have expired */ }
        }
    }
}
