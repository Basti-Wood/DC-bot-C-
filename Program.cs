using DCBot.Commands;
using DCBot.Core;
using DCBot.Core.Database;
using DCBot.Events;
using DCBot.Services.Ai;
using DCBot.Services.Level;
using DCBot.Services.Roleplay;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Load .env if present (Docker provides real env vars, .env is for local dev)
DotNetEnv.Env.TraversePath().Load();

var botConfig = BotConfig.FromEnvironment();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services.AddSingleton(botConfig);

// --- Database (MariaDB via Pomelo) ---
builder.Services.AddDbContextFactory<BotDbContext>(options =>
{
    options.UseMySql(
        botConfig.ConnectionString,
        new MariaDbServerVersion(new Version(11, 4, 0)));
});

// --- Discord client ---
builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
                   | GatewayIntents.GuildMessages
                   | GatewayIntents.GuildMembers
                   | GatewayIntents.GuildVoiceStates
                   | GatewayIntents.GuildMessageReactions
                   | GatewayIntents.MessageContent,
    AlwaysDownloadUsers = false,
    LogLevel = LogSeverity.Info,
}));

// --- Core: command handler + event manager ---
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddSingleton<EventManager>();

// --- Shared services (Services/) — usable by commands and events ---
builder.Services.AddSingleton<GuildConfigService>();
builder.Services.AddSingleton<LevelRepository>();
builder.Services.AddSingleton<RoleplayApi>();
builder.Services.AddSingleton<AiService>();

// --- The bot host service ---
builder.Services.AddHostedService<BotService>();

var host = builder.Build();

// Ensure database schema exists (with retry while MariaDB boots up)
await DatabaseInitializer.EnsureCreatedAsync(host.Services);

await host.RunAsync();
