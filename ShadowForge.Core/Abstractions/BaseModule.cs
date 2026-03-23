using Microsoft.Extensions.Logging;
using ShadowForge.Core.Interfaces;
using ShadowForge.Core.Models;

namespace ShadowForge.Core.Abstractions;

/// <summary>
/// Abstract base class for all ShadowForge modules.
/// Demonstrates: Template Method pattern, inheritance, encapsulation, polymorphism.
/// Concrete modules only override the protected abstract ExecuteCoreAsync() method.
/// The public ExecuteAsync() handles lifecycle, logging, timing, and error handling.
/// </summary>
public abstract class BaseModule : IModule
{
    protected readonly ILogger Logger;
    private ModuleStatus _status = ModuleStatus.Idle;

    protected BaseModule(ILogger logger)
    {
        Logger = logger;
    }

    // ── Abstract members — must be implemented by each concrete module ─────────
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract ModuleCategory Category { get; }

    // ── Virtual members — subclasses may override ─────────────────────────────
    public virtual IEnumerable<string> GetRequiredPermissions() => [];
    public virtual Task<bool> ValidateAsync(ModuleContext context) => Task.FromResult(true);

    // ── Public status (read-only externally) ──────────────────────────────────
    public ModuleStatus Status => _status;

    // ── Template Method: ExecuteAsync orchestrates the full lifecycle ─────────
    public async Task<ModuleResult> ExecuteAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ModuleResult
        {
            SessionId = context.SessionId,
            ModuleName = Name,
            Status = ModuleStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        _status = ModuleStatus.Running;
        Logger.LogInformation("[{Module}] Starting execution for session {SessionId}", Name, context.SessionId);

        try
        {
            if (!await ValidateAsync(context))
            {
                result.Status = ModuleStatus.Failed;
                result.ErrorMessage = "Validation failed — missing required parameters.";
                _status = ModuleStatus.Failed;
                return result;
            }

            // ── Delegate to the concrete module's implementation ──────────────
            await ExecuteCoreAsync(context, result, cancellationToken);

            result.Status = cancellationToken.IsCancellationRequested
                ? ModuleStatus.Cancelled
                : ModuleStatus.Completed;

            _status = result.Status;
            Logger.LogInformation("[{Module}] Completed in {Duration}ms with {EventCount} events",
                Name, result.Duration.TotalMilliseconds, result.Events.Count);
        }
        catch (OperationCanceledException)
        {
            result.Status = ModuleStatus.Cancelled;
            _status = ModuleStatus.Cancelled;
            Logger.LogWarning("[{Module}] Cancelled by user.", Name);
        }
        catch (Exception ex)
        {
            result.Status = ModuleStatus.Failed;
            result.ErrorMessage = ex.Message;
            _status = ModuleStatus.Failed;
            Logger.LogError(ex, "[{Module}] Execution failed.", Name);
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    // ── The hook: concrete modules implement their logic here ─────────────────
    protected abstract Task ExecuteCoreAsync(
        ModuleContext context,
        ModuleResult result,
        CancellationToken cancellationToken);

    // ── Protected helpers for subclasses ──────────────────────────────────────
    protected void AddEvent(ModuleResult result, SimulationEvent evt)
    {
        result.Events.Add(evt);
        Logger.LogDebug("[{Module}] Event: {Type} | {Source} → {Target} | {ThreatLevel}",
            Name, evt.Type, evt.Source, evt.Target, evt.ThreatLevel);
    }

    protected SimulationEvent CreateEvent(
        SimEventType type,
        string source,
        string target,
        string description,
        ThreatLevel threat = ThreatLevel.Low,
        string? mitreTechId = null,
        string? mitreTechName = null) => new()
        {
            Type = type,
            Source = source,
            Target = target,
            Description = description,
            ThreatLevel = threat,
            MitreTechniqueId = mitreTechId,
            MitreTechniqueName = mitreTechName
        };

    protected static async Task SimulateDelayAsync(int minMs, int maxMs, CancellationToken ct)
    {
        var delay = Random.Shared.Next(minMs, maxMs);
        await Task.Delay(delay, ct);
    }
}

/// <summary>
/// Base for modules that also implement IPersistentModule.
/// Combines template method with persistence responsibility.
/// </summary>
public abstract class PersistentBaseModule : BaseModule, IPersistentModule
{
    private readonly List<ModuleResult> _history = [];

    protected PersistentBaseModule(ILogger logger) : base(logger) { }

    public virtual Task SaveStateAsync(ModuleResult result, CancellationToken cancellationToken = default)
    {
        _history.Add(result);
        return Task.CompletedTask;
    }

    public virtual Task<IEnumerable<ModuleResult>> GetHistoryAsync(int count = 50)
        => Task.FromResult<IEnumerable<ModuleResult>>(_history.TakeLast(count).ToList());
}
