using DCBot.Core.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCBot.Core.Database;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

    public DbSet<UserLevel> UserLevels => Set<UserLevel>();
    public DbSet<GuildConfig> GuildConfigs => Set<GuildConfig>();
    public DbSet<RainbowUser> RainbowUsers => Set<RainbowUser>();
    public DbSet<GalleryThread> GalleryThreads => Set<GalleryThread>();
    public DbSet<GalleryPost> GalleryPosts => Set<GalleryPost>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserLevel>(e =>
        {
            e.ToTable("user_levels");
            e.HasIndex(x => new { x.BotId, x.GuildId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.BotId, x.GuildId, x.Xp });
        });

        modelBuilder.Entity<GuildConfig>(e =>
        {
            e.ToTable("guild_configs");
            e.HasIndex(x => new { x.BotId, x.GuildId, x.Key }).IsUnique();
        });

        modelBuilder.Entity<RainbowUser>(e =>
        {
            e.ToTable("rainbow_users");
            e.HasIndex(x => new { x.BotId, x.GuildId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.BotId, x.Active });
        });

        modelBuilder.Entity<GalleryThread>(e =>
        {
            e.ToTable("gallery_threads");
            e.HasIndex(x => new { x.BotId, x.GuildId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<GalleryPost>(e =>
        {
            e.ToTable("gallery_posts");
            e.HasIndex(x => new { x.BotId, x.GuildId, x.ChannelId, x.MessageId }).IsUnique();
        });
    }
}
