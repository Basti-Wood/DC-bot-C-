using System.ComponentModel.DataAnnotations;

namespace DCBot.Core.Database.Entities;

/// <summary>Rainbow-Modus (booster): Farb-Rolle rotiert alle 30 Minuten.</summary>
public class RainbowUser
{
    [Key]
    public long Id { get; set; }

    [MaxLength(64)]
    public required string BotId { get; set; }

    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public int CurrentIndex { get; set; }
    public bool Active { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
