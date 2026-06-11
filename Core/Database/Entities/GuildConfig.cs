using System.ComponentModel.DataAnnotations;

namespace DCBot.Core.Database.Entities;

/// <summary>
/// Generic per-guild key/value configuration (like the Go bot's config
/// collection): configured channels, roles, feature flags, ...
/// Example keys: "channel:bot", "channel:welcome", "channel:logs".
/// </summary>
public class GuildConfig
{
    [Key]
    public long Id { get; set; }

    [MaxLength(64)]
    public required string BotId { get; set; }

    public ulong GuildId { get; set; }

    [MaxLength(128)]
    public required string Key { get; set; }

    [MaxLength(512)]
    public required string Value { get; set; }
}
