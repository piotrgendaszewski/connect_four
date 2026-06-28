namespace ConnectFour.Data;

using Microsoft.EntityFrameworkCore;
using ConnectFour.Models;

public class AppDbContext : DbContext
{
    public DbSet<Player> Players { get; set; } = null!;
    public DbSet<GameRecord> GameRecords { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Player>().HasIndex(p => p.Nick).IsUnique();
    }
}
