namespace ShadowForge.Core.Models;

// ─── Enumerations ────────────────────────────────────────────────────────────

public enum ModuleCategory { Reconnaissance, LateralMovement, Persistence, Reporting, ThreatIntel }
public enum ModuleStatus    { Idle, Running, Completed, Failed, Cancelled }
public enum ThreatLevel     { None, Low, Medium, High, Critical }
public enum SimEventType    { HostDiscovered, PortScanned, PivotAttempted, PivotSucceeded, PersistenceEstablished, IOCDetected }
public enum OsType          { Windows, Linux, MacOS, Unknown }

// ─── Module execution models ─────────────────────────────────────────────────

public record ModuleContext(
    Guid SessionId,
    string TargetSubnet,
    IReadOnlyDictionary<string, string> Parameters
);

public class ModuleResult
{
    public Guid Id            { get; init; } = Guid.NewGuid();
    public Guid SessionId     { get; set; }
    public string ModuleName  { get; set; } = string.Empty;
    public ModuleStatus Status { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : TimeSpan.Zero;
    public List<SimulationEvent> Events { get; set; } = [];
    public Dictionary<string, object> Data { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public ThreatLevel HighestThreatLevel => Events.Any()
        ? Events.Max(e => e.ThreatLevel)
        : ThreatLevel.None;
}

public class ModuleEventArgs : EventArgs
{
    public SimulationEvent Event { get; init; } = null!;
    public string ModuleName    { get; init; } = string.Empty;
    public Guid SessionId       { get; init; }
}

// ─── Simulation event model ───────────────────────────────────────────────────

public class SimulationEvent
{
    public Guid Id              { get; init; } = Guid.NewGuid();
    public SimEventType Type    { get; set; }
    public ThreatLevel ThreatLevel { get; set; }
    public DateTime Timestamp   { get; set; } = DateTime.UtcNow;
    public string Source        { get; set; } = string.Empty;
    public string Target        { get; set; } = string.Empty;
    public string Description   { get; set; } = string.Empty;
    public string? MitreTechniqueId { get; set; }
    public string? MitreTechniqueName { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

// ─── Network / host models ────────────────────────────────────────────────────

public class SimulatedHost
{
    public Guid Id              { get; init; } = Guid.NewGuid();
    public string IpAddress     { get; set; } = string.Empty;
    public string Hostname      { get; set; } = string.Empty;
    public OsType OperatingSystem { get; set; }
    public string OsVersion     { get; set; } = string.Empty;
    public List<OpenPort> OpenPorts { get; set; } = [];
    public List<string> RunningServices { get; set; } = [];
    public bool IsCompromised   { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public ThreatLevel ThreatScore { get; set; }
}

public record OpenPort(int Port, string Protocol, string ServiceName, string? Banner);

public class NetworkScanResult
{
    public string Subnet            { get; set; } = string.Empty;
    public List<SimulatedHost> Hosts { get; set; } = [];
    public int TotalHostsFound      => Hosts.Count;
    public int OpenPortsFound       => Hosts.SelectMany(h => h.OpenPorts).Count();
    public TimeSpan ScanDuration    { get; set; }
    public DateTime ScannedAt       { get; set; } = DateTime.UtcNow;
}

public class LateralMovementResult
{
    public string FromHost          { get; set; } = string.Empty;
    public string ToHost            { get; set; } = string.Empty;
    public bool Success             { get; set; }
    public string Technique         { get; set; } = string.Empty;
    public string? MitreTechniqueId { get; set; }
    public List<string> StepsLog    { get; set; } = [];
    public DateTime AttemptedAt     { get; set; } = DateTime.UtcNow;
}

// ─── Threat intelligence models ───────────────────────────────────────────────

public class IpReputation
{
    public string IpAddress         { get; set; } = string.Empty;
    public int AbuseScore           { get; set; }       // 0-100
    public ThreatLevel ThreatLevel  { get; set; }
    public string? CountryCode      { get; set; }
    public string? Isp              { get; set; }
    public bool IsWhitelisted       { get; set; }
    public bool IsPubliclyRoutable  { get; set; }
    public List<string> AssociatedCampaigns { get; set; } = [];
    public List<string> Tags        { get; set; } = [];
    public DateTime CheckedAt       { get; set; } = DateTime.UtcNow;
}

public class ThreatIndicator
{
    public string Id                { get; set; } = string.Empty;
    public string Name              { get; set; } = string.Empty;
    public string Description       { get; set; } = string.Empty;
    public string Author            { get; set; } = string.Empty;
    public DateTime Modified        { get; set; }
    public List<string> Tags        { get; set; } = [];
    public int IndicatorCount       { get; set; }
    public ThreatLevel ThreatLevel  { get; set; }
}

public class DomainIntel
{
    public string Domain            { get; set; } = string.Empty;
    public bool IsMalicious         { get; set; }
    public ThreatLevel ThreatLevel  { get; set; }
    public List<string> Categories  { get; set; } = [];
    public string? RegistrarName    { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public List<string> AssociatedIps { get; set; } = [];
}

public class FileHashIntel
{
    public string Hash              { get; set; } = string.Empty;
    public string HashType          { get; set; } = "SHA256";
    public bool IsMalicious         { get; set; }
    public int DetectionCount       { get; set; }
    public int TotalEngines         { get; set; }
    public string? MalwareFamilyName { get; set; }
    public ThreatLevel ThreatLevel  { get; set; }
}

// ─── User generation models ───────────────────────────────────────────────────

public class FakeUser
{
    public Guid Id                  { get; init; } = Guid.NewGuid();
    public string FirstName         { get; set; } = string.Empty;
    public string LastName          { get; set; } = string.Empty;
    public string FullName          => $"{FirstName} {LastName}";
    public string Email             { get; set; } = string.Empty;
    public string Username          { get; set; } = string.Empty;
    public string Department        { get; set; } = string.Empty;
    public string JobTitle          { get; set; } = string.Empty;
    public string PhoneNumber       { get; set; } = string.Empty;
    public string AvatarUrl         { get; set; } = string.Empty;
    public string IpAddress         { get; set; } = string.Empty;
    public bool IsAdminAccount      { get; set; }
    public DateTime CreatedAt       { get; init; } = DateTime.UtcNow;
}

public record UserTemplate(string Department, string JobTitle, bool IsAdmin = false);

// ─── Reporting models ─────────────────────────────────────────────────────────

public class SimulationReport
{
    public Guid Id                  { get; init; } = Guid.NewGuid();
    public Guid SessionId           { get; set; }
    public string Title             { get; set; } = string.Empty;
    public DateTime GeneratedAt     { get; set; } = DateTime.UtcNow;
    public string Analyst           { get; set; } = "ShadowForge AutoReport";
    public ExecutiveSummary Summary { get; set; } = new();
    public List<ModuleResult> ModuleResults { get; set; } = [];
    public List<MitreAttackMapping> MitreMappings { get; set; } = [];
    public List<SimulatedHost> DiscoveredHosts { get; set; } = [];
    public List<ThreatIndicator> IOCsIdentified { get; set; } = [];
    public ThreatLevel OverallRiskLevel => ModuleResults.Any()
        ? ModuleResults.Max(m => m.HighestThreatLevel)
        : ThreatLevel.None;
}

public class ExecutiveSummary
{
    public string Overview          { get; set; } = string.Empty;
    public int TotalEventsGenerated { get; set; }
    public int HostsDiscovered      { get; set; }
    public int CriticalFindings     { get; set; }
    public List<string> KeyTakeaways { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
}

public class MitreAttackMapping
{
    public string TechniqueId       { get; set; } = string.Empty;
    public string TechniqueName     { get; set; } = string.Empty;
    public string Tactic            { get; set; } = string.Empty;
    public string Description       { get; set; } = string.Empty;
    public string Url               { get; set; } = string.Empty;
    public int TimesObserved        { get; set; }
    public ThreatLevel Severity     { get; set; }
}
