using ShadowForge.Core.Models;

namespace ShadowForge.Web.Services;

/// <summary>
/// Reactive state container for the Blazor dashboard.
/// Uses event notifications to trigger UI re-renders — no polling needed.
/// Demonstrates: Observer pattern, encapsulation, thread-safe collections.
/// </summary>
public sealed class DashboardStateService
{
    // ── State ─────────────────────────────────────────────────────────────────
    public Guid CurrentSessionId  { get; private set; } = Guid.NewGuid();
    public bool IsRunning         { get; private set; }
    public string CurrentModule   { get; private set; } = string.Empty;
    public double ProgressPercent { get; private set; }

    public List<SimulationEvent>  LiveEvents      { get; } = [];
    public List<SimulatedHost>    DiscoveredHosts { get; } = [];
    public List<ThreatIndicator>  IOCPulses       { get; } = [];
    public List<MitreAttackMapping> MitreMappings { get; } = [];
    public List<ModuleResult>     ModuleResults   { get; } = [];

    // Counters
    public int TotalEvents   => LiveEvents.Count;
    public int CriticalCount => LiveEvents.Count(e => e.ThreatLevel == ThreatLevel.Critical);
    public int HighCount     => LiveEvents.Count(e => e.ThreatLevel == ThreatLevel.High);
    public int HostCount     => DiscoveredHosts.Count;
    public int CompromisedCount => DiscoveredHosts.Count(h => h.IsCompromised);

    // ── Events (notify Blazor components to re-render) ─────────────────────────
    public event Action? OnStateChanged;
    public event Action<SimulationEvent>? OnNewEvent;
    public event Action<SimulatedHost>? OnHostDiscovered;

    // ── Mutations ─────────────────────────────────────────────────────────────

    public void StartSession(string subnet)
    {
        CurrentSessionId = Guid.NewGuid();
        IsRunning        = true;
        LiveEvents.Clear();
        DiscoveredHosts.Clear();
        MitreMappings.Clear();
        ModuleResults.Clear();
        NotifyStateChanged();
    }

    public void EndSession()
    {
        IsRunning       = false;
        CurrentModule   = string.Empty;
        ProgressPercent = 100;
        NotifyStateChanged();
    }

    public void AddEvent(SimulationEvent evt)
    {
        LiveEvents.Insert(0, evt); // newest first
        if (LiveEvents.Count > 500) LiveEvents.RemoveAt(LiveEvents.Count - 1); // cap at 500
        OnNewEvent?.Invoke(evt);

        // Auto-discover hosts from host-discovered events
        if (evt.Type == SimEventType.HostDiscovered &&
            !DiscoveredHosts.Any(h => h.IpAddress == evt.Target))
        {
            var host = new SimulatedHost
            {
                IpAddress   = evt.Target,
                Hostname    = evt.Metadata.GetValueOrDefault("hostname", evt.Target),
                OsVersion   = evt.Metadata.GetValueOrDefault("os", "Unknown"),
                ThreatScore = evt.ThreatLevel
            };
            DiscoveredHosts.Add(host);
            OnHostDiscovered?.Invoke(host);
        }

        if (evt.Type == SimEventType.PivotSucceeded)
        {
            var host = DiscoveredHosts.FirstOrDefault(h => h.IpAddress == evt.Target);
            if (host is not null) host.IsCompromised = true;
        }

        NotifyStateChanged();
    }

    public void UpdateProgress(string moduleName, double percent)
    {
        CurrentModule   = moduleName;
        ProgressPercent = percent;
        NotifyStateChanged();
    }

    public void SetModuleResult(ModuleResult result)
    {
        ModuleResults.RemoveAll(r => r.ModuleName == result.ModuleName);
        ModuleResults.Add(result);

        // Extract MITRE mappings
        if (result.Data.TryGetValue("mitre_mappings", out var raw) &&
            raw is List<MitreAttackMapping> mappings)
        {
            MitreMappings.Clear();
            MitreMappings.AddRange(mappings);
        }

        // Extract IOC pulses
        if (result.Data.TryGetValue("ioc_pulses", out var pulsesRaw) &&
            pulsesRaw is List<ThreatIndicator> pulses)
        {
            IOCPulses.Clear();
            IOCPulses.AddRange(pulses);
        }

        NotifyStateChanged();
    }

    public void SetIOCPulses(IEnumerable<ThreatIndicator> pulses)
    {
        IOCPulses.Clear();
        IOCPulses.AddRange(pulses);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
