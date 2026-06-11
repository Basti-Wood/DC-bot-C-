namespace DCBot.Core;

/// <summary>
/// Identity + environment configuration for ONE bot instance.
/// Multiple bots run from the same code; each container gets its own
/// BOT_ID and DISCORD_TOKEN. Every database row is keyed by BotId, so
/// bots never see each other's data.
/// </summary>
public sealed class BotConfig
{
    public required string BotId { get; init; }
    public required string Token { get; init; }
    public string? GuildId { get; init; }
    public required string ConnectionString { get; init; }
    public bool Debug { get; init; }

    public static BotConfig FromEnvironment()
    {
        var token = Env("DISCORD_TOKEN") ?? Env("TOKEN")
            ?? throw new InvalidOperationException(
                "Discord bot token not found. Set DISCORD_TOKEN (or TOKEN) in the environment / .env file.");

        var botId = Env("BOT_ID") ?? "main";

        var dbHost = Env("DB_HOST") ?? "localhost";
        var dbPort = Env("DB_PORT") ?? "3306";
        var dbName = Env("DB_NAME") ?? "dcbot";
        var dbUser = Env("DB_USER") ?? "dcbot";
        var dbPass = Env("DB_PASSWORD") ?? "dcbot";

        return new BotConfig
        {
            BotId = botId,
            Token = token,
            GuildId = Env("GUILD_ID"),
            Debug = Env("LOG_DEBUG") == "true",
            ConnectionString =
                $"Server={dbHost};Port={dbPort};Database={dbName};User={dbUser};Password={dbPass};",
        };
    }

    private static string? Env(string key)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}
