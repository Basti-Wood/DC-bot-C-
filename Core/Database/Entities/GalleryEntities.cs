using System.ComponentModel.DataAnnotations;

namespace DCBot.Core.Database.Entities;

/// <summary>Galerie: Forum-Thread eines Users (ein Thread pro User pro Guild).</summary>
public class GalleryThread
{
    [Key]
    public long Id { get; set; }

    [MaxLength(64)]
    public required string BotId { get; set; }

    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public ulong ThreadId { get; set; }
}

/// <summary>Galerie: Verknüpfung Original-Nachricht ↔ Galerie-Post im Thread.</summary>
public class GalleryPost
{
    [Key]
    public long Id { get; set; }

    [MaxLength(64)]
    public required string BotId { get; set; }

    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
    public ulong ThreadId { get; set; }
    public ulong PostId { get; set; }
}
