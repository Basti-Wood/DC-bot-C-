using System.ComponentModel.DataAnnotations;

namespace DCBot.Core.Database.Entities;

/// <summary>
/// XP / level entry for one user, in one guild, for one bot instance.
/// BotId keeps multiple bots sharing one database fully separated.
/// </summary>
public class UserLevel
{
    [Key]
    public long Id { get; set; }

    [MaxLength(64)]
    public required string BotId { get; set; }

    public ulong GuildId { get; set; }

    public ulong UserId { get; set; }

    public long Xp { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
