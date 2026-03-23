using Microsoft.EntityFrameworkCore;
using ShadowForge.Core.Models;

namespace ShadowForge.Data;

/// <summary>
/// Entity Framework Core database context.
/// Demonstrates: ORM, code-first migrations, fluent API configuration.
/// </summary>
public class ShadowForgeDbContext : DbContext
{
    public ShadowForgeDbContext(DbContextOptions<ShadowForgeDbContext> options) : base(options) { }

    public DbSet<SimulationSessionEntity> Sessions  { get; set; }
    public DbSet<SimEventEntity>          Events    { get; set; }
    public DbSet<DiscoveredHostEntity>    Hosts     { get; set; }
    public DbSet<FakeUserEntity>          Users     { get; set; }
    public DbSet<IocCacheEntity>          IocCache  { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SimulationSessionEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.SessionId).IsRequired();
            b.HasMany(e => e.Events).WithOne(ev => ev.Session).HasForeignKey(ev => ev.SessionEntityId);
            b.HasMany(e => e.Hosts).WithOne(h => h.Session).HasForeignKey(h => h.SessionEntityId);
            b.HasIndex(e => e.SessionId).IsUnique();
        });

        modelBuilder.Entity<SimEventEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<DiscoveredHostEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
        });

        modelBuilder.Entity<FakeUserEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<IocCacheEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.Indicator).IsUnique();
        });
    }
}

// ─── EF Core Entities (separate from domain models — anti-corruption layer) ───

public class SimulationSessionEntity
{
    public int Id              { get; set; }
    public Guid SessionId      { get; set; }
    public string Title        { get; set; } = string.Empty;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status       { get; set; } = "Running";
    public string Subnet       { get; set; } = string.Empty;
    public List<SimEventEntity>        Events { get; set; } = [];
    public List<DiscoveredHostEntity>  Hosts  { get; set; } = [];
}

public class SimEventEntity
{
    public int Id                  { get; set; }
    public int SessionEntityId     { get; set; }
    public SimulationSessionEntity Session { get; set; } = null!;
    public string EventType        { get; set; } = string.Empty;
    public string ThreatLevel      { get; set; } = string.Empty;
    public string Source           { get; set; } = string.Empty;
    public string Target           { get; set; } = string.Empty;
    public string Description      { get; set; } = string.Empty;
    public string? MitreTechniqueId { get; set; }
    public string? MitreTechniqueName { get; set; }
    public string ModuleName       { get; set; } = string.Empty;
    public DateTime Timestamp      { get; set; } = DateTime.UtcNow;
}

public class DiscoveredHostEntity
{
    public int Id                  { get; set; }
    public int SessionEntityId     { get; set; }
    public SimulationSessionEntity Session { get; set; } = null!;
    public string IpAddress        { get; set; } = string.Empty;
    public string Hostname         { get; set; } = string.Empty;
    public string OperatingSystem  { get; set; } = string.Empty;
    public string OsVersion        { get; set; } = string.Empty;
    public string OpenPorts        { get; set; } = string.Empty; // JSON-serialised
    public bool IsCompromised      { get; set; }
    public string ThreatLevel      { get; set; } = string.Empty;
    public DateTime DiscoveredAt   { get; set; } = DateTime.UtcNow;
}

public class FakeUserEntity
{
    public int Id              { get; set; }
    public string FirstName    { get; set; } = string.Empty;
    public string LastName     { get; set; } = string.Empty;
    public string Email        { get; set; } = string.Empty;
    public string Username     { get; set; } = string.Empty;
    public string Department   { get; set; } = string.Empty;
    public string JobTitle     { get; set; } = string.Empty;
    public string IpAddress    { get; set; } = string.Empty;
    public string AvatarUrl    { get; set; } = string.Empty;
    public bool IsAdmin        { get; set; }
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
}

public class IocCacheEntity
{
    public int Id              { get; set; }
    public string Indicator    { get; set; } = string.Empty; // IP, domain, hash
    public string IndicatorType { get; set; } = string.Empty;
    public int AbuseScore      { get; set; }
    public string ThreatLevel  { get; set; } = string.Empty;
    public string TagsJson     { get; set; } = "[]";
    public DateTime CachedAt   { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt  { get; set; }
}
