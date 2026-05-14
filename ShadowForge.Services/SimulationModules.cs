using Microsoft.Extensions.Logging;
using ShadowForge.Core.Abstractions;
using ShadowForge.Core.Interfaces;
using ShadowForge.Core.Models;

namespace ShadowForge.Services;

// ─── 1. Reconnaissance Module ─────────────────────────────────────────────────
/// <summary>
/// Simulates network reconnaissance using live threat intel enrichment.
/// Overrides BaseModule.ExecuteCoreAsync — demonstrates polymorphism in action.
/// </summary>
public sealed class ReconModule : PersistentBaseModule, IRealtimeModule
{
    private readonly INetworkSimService _networkSim;
    private readonly IThreatIntelService _threatIntel;

    public override string Name        => "Reconnaissance";
    public override string Description => "Network discovery, port enumeration, and live IOC enrichment";
    public override ModuleCategory Category => ModuleCategory.Reconnaissance;

    public event EventHandler<ModuleEventArgs>? OnEventEmitted;

    public ReconModule(
        INetworkSimService networkSim,
        IThreatIntelService threatIntel,
        ILogger<ReconModule> logger) : base(logger)
    {
        _networkSim  = networkSim;
        _threatIntel = threatIntel;
    }

    public override IEnumerable<string> GetRequiredPermissions()
        => ["network.scan", "threatintel.read"];

    protected override async Task ExecuteCoreAsync(
        ModuleContext context,
        ModuleResult result,
        CancellationToken ct)
    {
        var subnet = context.Parameters.GetValueOrDefault("subnet", "10.0.1.0/24");
        result.Data["subnet"] = subnet;

        // Phase 1: Network discovery
        Logger.LogInformation("[Recon] Starting network discovery on {Subnet}", subnet);
        var scanResult = await _networkSim.SimulateScanAsync(subnet, ct);
        result.Data["scan_result"] = scanResult;

        foreach (var host in scanResult.Hosts)
        {
            var evt = CreateEvent(
                SimEventType.HostDiscovered,
                source: "scanner",
                target: host.IpAddress,
                description: $"Host {host.Hostname} ({host.IpAddress}) — {host.OperatingSystem} — {host.OpenPorts.Count} open ports",
                threat: host.ThreatScore,
                mitreTechId: "T1046",
                mitreTechName: "Network Service Discovery"
            );
            evt.Metadata["hostname"] = host.Hostname;
            evt.Metadata["os"]       = host.OsVersion;
            evt.Metadata["ports"]    = string.Join(",", host.OpenPorts.Select(p => p.Port));
            AddEvent(result, evt);

            // Emit real-time event for SignalR
            OnEventEmitted?.Invoke(this, new ModuleEventArgs
            {
                Event      = evt,
                ModuleName = Name,
                SessionId  = context.SessionId
            });
        }

        // Phase 2: Threat intel enrichment on discovered IPs
        Logger.LogInformation("[Recon] Enriching {Count} hosts with live threat intel...", scanResult.Hosts.Count);

        // Run up to 5 parallel IP checks (respect free API rate limits)
        var checkTasks = scanResult.Hosts
            .Take(5)
            .Select(h => EnrichHostAsync(h, result, context.SessionId));

        await Task.WhenAll(checkTasks);

        // Phase 3: Fetch live IOC pulses
        var pulses = (await _threatIntel.GetLatestPulsesAsync(10)).ToList();
        result.Data["ioc_pulses"] = pulses;

        Logger.LogInformation("[Recon] Phase complete — {Hosts} hosts, {Pulses} IOC feeds",
            scanResult.Hosts.Count, pulses.Count);
    }

    private async Task EnrichHostAsync(SimulatedHost host, ModuleResult result, Guid sessionId)
    {
        var rep = await _threatIntel.CheckIpReputationAsync(host.IpAddress);
        if (rep.ThreatLevel >= ThreatLevel.Medium)
        {
            var evt = CreateEvent(
                SimEventType.IOCDetected,
                source: "threat_intel",
                target: host.IpAddress,
                description: $"IP flagged: AbuseIPDB score {rep.AbuseScore}/100 — {string.Join(", ", rep.Tags.Take(3))}",
                threat: rep.ThreatLevel,
                mitreTechId: "T1590",
                mitreTechName: "Gather Victim Network Information"
            );
            evt.Metadata["abuse_score"] = rep.AbuseScore.ToString();
            evt.Metadata["isp"]         = rep.Isp ?? "unknown";
            AddEvent(result, evt);
            OnEventEmitted?.Invoke(this, new ModuleEventArgs { Event = evt, ModuleName = Name, SessionId = sessionId });
        }
    }
}

// ─── 2. Lateral Movement Module ───────────────────────────────────────────────
/// <summary>
/// Simulates adversary pivoting between discovered hosts.
/// Demonstrates: chained async operations, event-driven architecture.
/// </summary>
public sealed class LateralMovementModule : PersistentBaseModule, IRealtimeModule
{
    private readonly INetworkSimService _networkSim;

    public override string Name        => "Lateral Movement";
    public override string Description => "Simulates pivot attempts between discovered hosts via WMI, SMB, RDP, WinRM";
    public override ModuleCategory Category => ModuleCategory.LateralMovement;

    public event EventHandler<ModuleEventArgs>? OnEventEmitted;

    public LateralMovementModule(INetworkSimService networkSim, ILogger<LateralMovementModule> logger)
        : base(logger)
    {
        _networkSim = networkSim;
    }

    protected override async Task ExecuteCoreAsync(ModuleContext context, ModuleResult result, CancellationToken ct)
    {
        var hosts = (await _networkSim.GetDiscoveredHostsAsync()).ToList();

        if (hosts.Count < 2)
        {
            result.ErrorMessage = "Not enough discovered hosts. Run Reconnaissance first.";
            result.Status = ModuleStatus.Failed;
            return;
        }

        // Simulate pivoting through a chain of hosts
        var pivotChain = hosts.OrderBy(_ => Random.Shared.Next()).Take(Math.Min(hosts.Count, 6)).ToList();

        for (int i = 0; i < pivotChain.Count - 1 && !ct.IsCancellationRequested; i++)
        {
            var from = pivotChain[i];
            var to   = pivotChain[i + 1];

            await SimulateDelayAsync(300, 800, ct);
            var pivotResult = await _networkSim.SimulatePivotAsync(from.IpAddress, to.IpAddress);

            var eventType = pivotResult.Success ? SimEventType.PivotSucceeded : SimEventType.PivotAttempted;
            var threat    = pivotResult.Success ? ThreatLevel.High : ThreatLevel.Medium;

            var evt = CreateEvent(
                eventType,
                source: from.IpAddress,
                target: to.IpAddress,
                description: pivotResult.Success
                    ? $"Pivot succeeded via {pivotResult.Technique} — session established"
                    : $"Pivot failed via {pivotResult.Technique} — authentication denied",
                threat: threat,
                mitreTechId: pivotResult.MitreTechniqueId,
                mitreTechName: pivotResult.Technique
            );
            evt.Metadata["steps"] = string.Join("|", pivotResult.StepsLog);
            AddEvent(result, evt);

            OnEventEmitted?.Invoke(this, new ModuleEventArgs { Event = evt, ModuleName = Name, SessionId = context.SessionId });

            if (pivotResult.Success) to.IsCompromised = true;
        }

        result.Data["pivot_chain"] = pivotChain.Select(h => h.IpAddress).ToList();
        result.Data["compromised_count"] = pivotChain.Count(h => h.IsCompromised);
    }
}

// ─── 3. Persistence Module ────────────────────────────────────────────────────
/// <summary>
/// Simulates persistence techniques: scheduled tasks, registry modifications, startup items.
/// Fully conceptual/educational — logs technique details and MITRE mappings.
/// </summary>
public sealed class PersistenceModule : PersistentBaseModule, IRealtimeModule
{
    public override string Name        => "Persistence";
    public override string Description => "Simulates persistence establishment via scheduled tasks, registry, and startup mechanisms";
    public override ModuleCategory Category => ModuleCategory.Persistence;

    public event EventHandler<ModuleEventArgs>? OnEventEmitted;

    private static readonly (string Id, string Name, string Technique, ThreatLevel Level)[] PersistenceTechniques =
    [
        ("T1053.005", "Scheduled Task",        "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Schedule\\TaskCache", ThreatLevel.High),
        ("T1547.001", "Registry Run Keys",     "HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", ThreatLevel.High),
        ("T1543.003", "Windows Service",       "sc.exe create ShadowSvc binPath=\"C:\\Windows\\Temp\\agent.exe\"", ThreatLevel.Critical),
        ("T1136.001", "Local Account Creation","net user shadowagent P@ssw0rd! /add && net localgroup administrators shadowagent /add", ThreatLevel.Critical),
        ("T1037.001", "Logon Script (Win)",    "HKCU\\Environment\\UserInitMprLogonScript = cmd.exe /c agent.exe", ThreatLevel.Medium),
    ];

    public PersistenceModule(ILogger<PersistenceModule> logger) : base(logger) { }

    protected override async Task ExecuteCoreAsync(ModuleContext context, ModuleResult result, CancellationToken ct)
    {
        // Pick 2-3 random techniques to "demonstrate"
        var selected = PersistenceTechniques
            .OrderBy(_ => Random.Shared.Next())
            .Take(Random.Shared.Next(2, 4))
            .ToList();

        foreach (var tech in selected)
        {
            if (ct.IsCancellationRequested) break;
            await SimulateDelayAsync(400, 900, ct);

            var evt = CreateEvent(
                SimEventType.PersistenceEstablished,
                source: "localhost",
                target: "target_host",
                description: $"[SIMULATED] {tech.Name}: {tech.Technique}",
                threat: tech.Level,
                mitreTechId: tech.Id,
                mitreTechName: tech.Name
            );
            evt.Metadata["command_simulation"] = tech.Technique;
            evt.Metadata["registry_path"]      = tech.Technique.StartsWith("HK") ? tech.Technique : "N/A";

            AddEvent(result, evt);
            OnEventEmitted?.Invoke(this, new ModuleEventArgs { Event = evt, ModuleName = Name, SessionId = context.SessionId });
        }

        result.Data["techniques_simulated"] = selected.Select(t => t.Id).ToList();
    }
}

// ─── 4. Report Module ─────────────────────────────────────────────────────────
/// <summary>
/// Aggregates all module results into a structured simulation report.
/// Demonstrates: LINQ aggregation, composite pattern.
/// </summary>
public sealed class ReportModule : BaseModule
{
    public override string Name        => "Report Generator";
    public override string Description => "Aggregates simulation data into an executive-ready report with MITRE ATT&CK mappings";
    public override ModuleCategory Category => ModuleCategory.Reporting;

    private static readonly Dictionary<string, (string Tactic, string Description)> MitreDb = new()
    {
        ["T1046"]     = ("Discovery",          "Network Service Discovery"),
        ["T1590"]     = ("Reconnaissance",     "Gather Victim Network Information"),
        ["T1021.001"] = ("Lateral Movement",   "Remote Services: Remote Desktop Protocol"),
        ["T1021.002"] = ("Lateral Movement",   "Remote Services: SMB/Windows Admin Shares"),
        ["T1021.006"] = ("Lateral Movement",   "Remote Services: Windows Remote Management"),
        ["T1047"]     = ("Execution",          "Windows Management Instrumentation"),
        ["T1053.005"] = ("Persistence",        "Scheduled Task/Job: Scheduled Task"),
        ["T1547.001"] = ("Persistence",        "Boot or Logon Autostart: Registry Run Keys"),
        ["T1543.003"] = ("Persistence",        "Create or Modify System Process: Windows Service"),
        ["T1136.001"] = ("Persistence",        "Create Account: Local Account"),
        ["T1563.001"] = ("Lateral Movement",   "Remote Service Session Hijacking: SSH Hijacking"),
    };

    public ReportModule(ILogger<ReportModule> logger) : base(logger) { }

    protected override async Task ExecuteCoreAsync(ModuleContext context, ModuleResult result, CancellationToken ct)
    {
        await SimulateDelayAsync(200, 500, ct);

        var allEvents = SimulationEventStore.Get(context.SessionId);
       
        // Build MITRE ATT&CK mappings from all events via LINQ
        var allMitreTechniques = allEvents
            .Where(e => e.MitreTechniqueId is not null)
            .GroupBy(e => e.MitreTechniqueId!)
            .Select(g =>
            {
                var dbEntry = MitreDb.GetValueOrDefault(g.Key, ("Unknown", "Unknown technique"));
                return new MitreAttackMapping
                {
                    TechniqueId   = g.Key,
                    TechniqueName = g.First().MitreTechniqueName ?? dbEntry.Item2,
                    Tactic        = dbEntry.Item1,
                    Description   = dbEntry.Item2,
                    Url           = $"https://attack.mitre.org/techniques/{g.Key.Replace(".", "/")}/",
                    TimesObserved = g.Count(),
                    Severity      = g.Max(e => e.ThreatLevel)
                };
            })
            .OrderByDescending(m => m.TimesObserved)
            .ToList();

        result.Data["mitre_mappings"]   = allMitreTechniques;
        result.Data["total_events"]     = result.Events.Count;
        result.Data["critical_events"]  = result.Events.Count(e => e.ThreatLevel == ThreatLevel.Critical);
        result.Data["high_events"]      = result.Events.Count(e => e.ThreatLevel == ThreatLevel.High);

        Logger.LogInformation($"[Report] Generated report with {allMitreTechniques.Count} unique MITRE techniques");
    }
}


public static class SimulationEventStore
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, List<SimulationEvent>> _store = new();
    public static void Set(Guid sessionId, List<SimulationEvent> events) => _store[sessionId] = events;
    public static List<SimulationEvent> Get(Guid sessionId) => _store.TryGetValue(sessionId, out var e) ? e : new();
}
