using Microsoft.EntityFrameworkCore;

namespace PlumbobForge.Backend.Database;

public class AppDbContext : DbContext
{
    public DbSet<MetaEntity> MetaEntities { get; set; }
    public DbSet<SetsEntity> SetsEntities { get; set; }
    public DbSet<ConfigEntity> ConfigEntities { get; set; }
    public DbSet<ConfigSetsEntity> ConfigSetsEntities { get; set; }
    public DbSet<SettingEntity> SettingEntities { get; set; }
    public DbSet<TombstoneEntity> Tombstones { get; set; }
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SetsEntity>()
            .HasOne(s => s.ParentSetsEntity)
            .WithMany(s => s.Children)
            .HasForeignKey(s => s.ParentSetsEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MetaEntity>()
            .HasOne(m => m.SetsEntity)
            .WithMany(s => s.MetaEntities)
            .HasForeignKey(m => m.SetsEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MetaEntity>()
            .HasIndex(m => m.SetsEntityId);

        modelBuilder.Entity<MetaEntity>()
            .HasIndex(m => m.Enabled);

        modelBuilder.Entity<MetaEntity>()
            .HasIndex(m => m.PackageType);

        modelBuilder.Entity<MetaEntity>()
            .HasIndex(m => m.FileName);

        modelBuilder.Entity<ConfigSetsEntity>()
            .HasOne(c => c.ConfigEntity)
            .WithMany(ce => ce.ConfigSetsEntities)
            .HasForeignKey(c => c.ConfigEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ConfigSetsEntity>()
            .HasOne(c => c.SetsEntity)
            .WithMany()
            .HasForeignKey(c => c.SetsEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SettingEntity>()
            .HasOne(s => s.ConfigEntity)
            .WithMany(c => c.SettingEntities)
            .HasForeignKey(s => s.ConfigEntityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
