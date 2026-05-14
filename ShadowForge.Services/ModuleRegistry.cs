using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowForge.Core.Interfaces;
using ShadowForge.Core.Models;

namespace ShadowForge.Services;

/// <summary>
/// Plugin registry and factory for all ShadowForge modules.
/// Demonstrates: Factory pattern, Registry pattern, dependency injection, reflection-ready design.
/// New modules are registered here â€” the rest of the app is completely unaware of concrete types.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly Dictionary<string, Func<IModule>> _factories = [];
    private readonly ILogger<ModuleRegistry> _logger;

    public ModuleRegistry(IServiceProvider services, ILogger<ModuleRegistry> logger)
    {
        _logger = logger;
        RegisterDefaults(services);
    }

    // â”€â”€ Registration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public void Register(string name, Func<IModule> factory)
    {
        _factories[name] = factory;
        _logger.LogDebug("Module registered: {Name}", name);
    }

    private void RegisterDefaults(IServiceProvider services)
    {
        Register("Reconnaissance",      () => services.GetRequiredService<ReconModule>());
        Register("Lateral Movement",    () => services.GetRequiredService<LateralMovementModule>());
        Register("Persistence",         () => services.GetRequiredService<PersistenceModule>());
        Register("Report Generator",    () => services.GetRequiredService<ReportModule>());
    }

    // â”€â”€ Resolution â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public IModule Resolve(string name)
    {
        if (!_factories.TryGetValue(name, out var factory))
            throw new KeyNotFoundException($"Module '{name}' is not registered.");
        return factory();
    }

    public IEnumerable<IModule> ResolveAll()
        => _factories.Values.Select(f => f());

    public IEnumerable<IModule> ResolveByCategory(ModuleCategory category)
        => ResolveAll().Where(m => m.Category == category);

    public IEnumerable<string> GetRegisteredNames()
        => _factories.Keys;

    public bool IsRegistered(string name)
        => _factories.ContainsKey(name);
}

/// <summary>
/// Orchestrates running multiple modules in sequence or parallel for a full simulation.
/// Demonstrates: orchestration pattern, event aggregation, async coordination.
/// </summary>
public sealed class SimulationOrchestrator
{
    private readonly ModuleRegistry _registry;
    private readonly ILogger<SimulationOrchestrator> _logger;

    public event EventHandler<ModuleEventArgs>? OnModuleEvent;
    public event EventHandler<SimulationProgressEvent>? OnProgressUpdate;

    public SimulationOrchestrator(ModuleRegistry registry, ILogger<SimulationOrchestrator> logger)
    {
        _registry = registry;
        _logger   = logger;
    }

    /// <summary>Runs the full simulation pipeline sequentially.</summary>
    public async Task<List<ModuleResult>> RunFullSimulationAsync(
        ModuleContext context,
        CancellationToken ct = default)
    {
        var results  = new List<ModuleResult>();
        var modules  = new[] { "Reconnaissance", "Lateral Movement", "Persistence", "Report Generator" };
        var total    = modules.Length;

        _logger.LogInformation("Starting full simulation â€” session {SessionId}", context.SessionId);

        for (int i = 0; i < modules.Length && !ct.IsCancellationRequested; i++)
        {
            var moduleName = modules[i];
            var module     = _registry.Resolve(moduleName);

            // Wire up real-time events if the module supports it
            if (module is IRealtimeModule realtimeModule)
                realtimeModule.OnEventEmitted += (s, e) => OnModuleEvent?.Invoke(s, e);

            OnProgressUpdate?.Invoke(this, new SimulationProgressEvent(moduleName, i + 1, total, "Running"));

            var result = await module.ExecuteAsync(context, ct);
            results.Add(result);
            SimulationEventStore.Set(context.SessionId, results.SelectMany(r => r.Events).ToList());
            Console.WriteLine("[REGISTRY] Set sessionId=" + context.SessionId + " totalEvents=" + results.SelectMany(r => r.Events).Count());
            _logger.LogInformation("Module '{Module}' finished: {Status}", moduleName, result.Status);
        }

        return results;
    }

    /// <summary>Runs a single named module.</summary>
   public async Task<ModuleResult> RunModuleAsync(string moduleName, ModuleContext context, CancellationToken ct = default)
{
    var module = _registry.Resolve(moduleName);

    if (module is IRealtimeModule rt)
        rt.OnEventEmitted += (s, e) => OnModuleEvent?.Invoke(s, e);

    var result = await module.ExecuteAsync(context, ct);

    var existing = SimulationEventStore.Get(context.SessionId);
    existing.AddRange(result.Events);
    SimulationEventStore.Set(context.SessionId, existing);

return result;
    }
}

public record SimulationProgressEvent(string ModuleName, int Current, int Total, string Status)
{
    public double ProgressPercent => (double)Current / Total * 100;
}