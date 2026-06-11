using DCBot.Core.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCBot.Core.Database;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

    public DbSet<UserLevel> UserLevels => Set<UserLevel>();
    public DbSet<GuildConfig> GuildConfigs => Set<GuildConfig>();

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
    }
}
