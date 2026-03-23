using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShadowForge.Core.Interfaces;

namespace ShadowForge.Data.Repositories;

/// <summary>
/// Generic repository implementation over EF Core.
/// Demonstrates: Generics with constraints, async CRUD, separation of concerns.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ShadowForgeDbContext Context;
    protected readonly DbSet<T> DbSet;
    protected readonly ILogger Logger;

    public Repository(ShadowForgeDbContext context, ILogger logger)
    {
        Context = context;
        DbSet   = context.Set<T>();
        Logger  = logger;
    }

    public virtual async Task<T?> GetByIdAsync(int id)
        => await DbSet.FindAsync(id);

    public virtual async Task<IEnumerable<T>> GetAllAsync()
        => await DbSet.ToListAsync();

    public virtual async Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate)
        => await Task.FromResult(DbSet.AsEnumerable().Where(predicate).ToList());

    public virtual async Task<T> AddAsync(T entity)
    {
        await DbSet.AddAsync(entity);
        await Context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity)
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity is not null)
        {
            DbSet.Remove(entity);
            await Context.SaveChangesAsync();
        }
    }

    public virtual async Task<int> CountAsync()
        => await DbSet.CountAsync();
}

/// <summary>
/// Specialised repository for simulation sessions — includes eager loading.
/// Demonstrates: Repository inheritance and specialisation.
/// </summary>
public class SessionRepository : Repository<SimulationSessionEntity>
{
    public SessionRepository(ShadowForgeDbContext context, ILogger<SessionRepository> logger)
        : base(context, logger) { }

    public async Task<SimulationSessionEntity?> GetWithDetailsAsync(Guid sessionId)
        => await Context.Sessions
            .Include(s => s.Events)
            .Include(s => s.Hosts)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

    public async Task<IEnumerable<SimulationSessionEntity>> GetRecentAsync(int count = 10)
        => await Context.Sessions
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync();

    public async Task<SimulationSessionEntity> StartSessionAsync(Guid sessionId, string subnet)
    {
        var session = new SimulationSessionEntity
        {
            SessionId = sessionId,
            Title     = $"Simulation — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
            Subnet    = subnet,
            Status    = "Running"
        };
        return await AddAsync(session);
    }

    public async Task CompleteSessionAsync(Guid sessionId)
    {
        var session = await Context.Sessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session is null) return;
        session.Status      = "Completed";
        session.CompletedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();
    }
}

/// <summary>
/// Repository for simulation events with LINQ-powered analytics queries.
/// </summary>
public class EventRepository : Repository<SimEventEntity>
{
    public EventRepository(ShadowForgeDbContext context, ILogger<EventRepository> logger)
        : base(context, logger) { }

    public async Task<IEnumerable<SimEventEntity>> GetBySessionAsync(int sessionEntityId)
        => await Context.Events
            .Where(e => e.SessionEntityId == sessionEntityId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();

    public async Task<Dictionary<string, int>> GetThreatLevelCountsAsync(int sessionEntityId)
        => await Context.Events
            .Where(e => e.SessionEntityId == sessionEntityId)
            .GroupBy(e => e.ThreatLevel)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

    public async Task<IEnumerable<SimEventEntity>> GetCriticalEventsAsync()
        => await Context.Events
            .Where(e => e.ThreatLevel == "Critical")
            .OrderByDescending(e => e.Timestamp)
            .Take(50)
            .ToListAsync();

    public async Task AddBatchAsync(IEnumerable<SimEventEntity> events)
    {
        await Context.Events.AddRangeAsync(events);
        await Context.SaveChangesAsync();
    }
}
