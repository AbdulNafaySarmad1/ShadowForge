using ShadowForge.Core.Models;

namespace ShadowForge.Core.Interfaces;

/// <summary>
/// Core interface every ShadowForge module must implement.
/// Demonstrates: Interface segregation, dependency inversion, polymorphism.
/// </summary>
public interface IModule
{
    string Name { get; }
    string Description { get; }
    ModuleCategory Category { get; }
    ModuleStatus Status { get; }

    Task<ModuleResult> ExecuteAsync(ModuleContext context, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(ModuleContext context);
    IEnumerable<string> GetRequiredPermissions();
}

/// <summary>
/// Modules that pull live data from external APIs.
/// </summary>
public interface IApiModule : IModule
{
    string ApiEndpoint { get; }
    Task<bool> TestConnectivityAsync();
}

/// <summary>
/// Modules that persist simulation state to the database.
/// </summary>
public interface IPersistentModule : IModule
{
    Task SaveStateAsync(ModuleResult result, CancellationToken cancellationToken = default);
    Task<IEnumerable<ModuleResult>> GetHistoryAsync(int count = 50);
}

/// <summary>
/// Modules that emit real-time events via SignalR.
/// </summary>
public interface IRealtimeModule : IModule
{
    event EventHandler<ModuleEventArgs> OnEventEmitted;
}
