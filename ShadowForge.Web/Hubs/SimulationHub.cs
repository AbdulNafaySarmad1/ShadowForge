using Microsoft.AspNetCore.SignalR;
using ShadowForge.Core.Models;

namespace ShadowForge.Web.Hubs;

/// <summary>
/// SignalR hub that pushes real-time simulation events to all connected Blazor clients.
/// Demonstrates: Observer pattern at network layer, async push, group management.
/// </summary>
public sealed class SimulationHub : Hub
{
    private readonly ILogger<SimulationHub> _logger;

    public SimulationHub(ILogger<SimulationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "dashboard");
        await base.OnDisconnectedAsync(exception);
    }

    // Called server-side via IHubContext<SimulationHub>
    public static class Methods
    {
        public const string OnSimEvent     = "onSimEvent";
        public const string OnProgress     = "onProgress";
        public const string OnModuleStart  = "onModuleStart";
        public const string OnModuleEnd    = "onModuleEnd";
        public const string OnHostAdded    = "onHostAdded";
        public const string OnIocDetected  = "onIocDetected";
    }
}

/// <summary>
/// Service used by Blazor components to broadcast events to the hub.
/// Wraps IHubContext so services don't take a direct hub dependency.
/// </summary>
public sealed class SimulationBroadcaster
{
    private readonly IHubContext<SimulationHub> _hub;

    public SimulationBroadcaster(IHubContext<SimulationHub> hub) => _hub = hub;

    public Task BroadcastEventAsync(SimulationEvent evt, string moduleName)
        => _hub.Clients.Group("dashboard").SendAsync(
            SimulationHub.Methods.OnSimEvent,
            new
            {
                evt.Id,
                Type          = evt.Type.ToString(),
                evt.Source,
                evt.Target,
                evt.Description,
                ThreatLevel   = evt.ThreatLevel.ToString(),
                evt.MitreTechniqueId,
                evt.MitreTechniqueName,
                evt.Timestamp,
                ModuleName    = moduleName
            });

    public Task BroadcastProgressAsync(string moduleName, int current, int total, string status)
        => _hub.Clients.Group("dashboard").SendAsync(
            SimulationHub.Methods.OnProgress,
            new { ModuleName = moduleName, Current = current, Total = total, Status = status, Percent = (double)current / total * 100 });

    public Task BroadcastHostDiscoveredAsync(SimulatedHost host)
        => _hub.Clients.Group("dashboard").SendAsync(
            SimulationHub.Methods.OnHostAdded,
            new
            {
                host.IpAddress,
                host.Hostname,
                OS          = host.OperatingSystem.ToString(),
                host.OsVersion,
                Ports       = host.OpenPorts.Select(p => p.Port).ToList(),
                ThreatLevel = host.ThreatScore.ToString(),
                host.IsCompromised
            });
}
