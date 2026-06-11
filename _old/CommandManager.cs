using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Core;

/// <summary>
/// Central command manager. All modules register their slash commands here.
/// The manager publishes them to the guild (bulk overwrite, like the Go bot)
/// and dispatches incoming interactions to the right handler, enforcing
/// cooldowns and permissions.
/// </summary>
public sealed class CommandManager
{
    private readonly Dictionary<string, SlashCommand> _commands = new();
    private readonly ConcurrentDictionary<string, long> _cooldowns = new();
    private readonly ILogger<CommandManager> _logger;

    public CommandManager(ILogger<CommandManager> logger)
    {
        _logger = logger;
    }

    public IReadOnlyDictionary<string, SlashCommand> Commands => _commands;

    /// <summary>Register a command. Call this from your module's RegisterAsync.</summary>
    public void Register(SlashCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Command needs a name", nameof(command));

        if (!_commands.TryAdd(command.Name, command))
            throw new InvalidOperationException($"Command '{command.Name}' is already registered");

        _logger.LogDebug("Registered command /{Name}", command.Name);
    }

    /// <summary>
    /// Publish all registered commands to a guild (bulk overwrite),
    /// replacing whatever was registered before.
    /// </summary>
    public async Task PublishToGuildAsync(SocketGuild guild)
    {
        var props = _commands.Values
            .Select(c =>
            {
                var b = new SlashCommandBuilder()
                    .WithName(c.Name)
                    .WithDescription(c.Description);
                foreach (var opt in c.Options)
                    b.AddOption(opt);
                return (ApplicationCommandProperties)b.Build();
            })
            .ToArray();

        await guild.BulkOverwriteApplicationCommandAsync(props);
        _logger.LogInformation("Published {Count} commands to guild {Guild} ({Id})",
            props.Length, guild.Name, guild.Id);
    }

    /// <summary>Dispatch an incoming slash command interaction.</summary>
    public async Task HandleAsync(SocketSlashCommand interaction)
    {
        if (!_commands.TryGetValue(interaction.CommandName, out var cmd))
        {
            await interaction.RespondAsync("This command no longer exists.", ephemeral: true);
            return;
        }

        // --- permissions ---
        var guildUser = interaction.User as SocketGuildUser;
        var isAdmin = guildUser?.GuildPermissions.Administrator ?? false;

        if (!(cmd.AllowEveryone || (cmd.AllowAdmin && isAdmin)))
        {
            await interaction.RespondAsync("You do not have permission for this command.", ephemeral: true);
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
                    $"You have a {expires - now} second cooldown.", ephemeral: true);
                return;
            }
            _cooldowns[key] = now + cmd.Cooldown;
        }

        // --- execute ---
        try
        {
            await cmd.Handler(interaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command /{Name} failed", cmd.Name);
            try
            {
                if (!interaction.HasResponded)
                    await interaction.RespondAsync("An internal error occurred.", ephemeral: true);
                else
                    await interaction.FollowupAsync("An internal error occurred.", ephemeral: true);
            }
            catch { /* interaction may have expired */ }
        }
    }
}
