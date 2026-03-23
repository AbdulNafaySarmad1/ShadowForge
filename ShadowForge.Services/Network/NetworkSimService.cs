using Microsoft.Extensions.Logging;
using ShadowForge.Core.Interfaces;
using ShadowForge.Core.Models;

namespace ShadowForge.Services.Network;

/// <summary>
/// Simulates enterprise network scanning and lateral movement.
/// Uses realistic port/service combinations and OS fingerprinting data.
/// Demonstrates: async streams, cancellation tokens, value objects.
/// </summary>
public sealed class NetworkSimService : INetworkSimService
{
    private readonly ILogger<NetworkSimService> _logger;
    private readonly List<SimulatedHost> _discoveredHosts = [];

    // Realistic enterprise service maps
    private static readonly Dictionary<int, string> CommonPorts = new()
    {
        { 22,   "SSH" },        { 23,  "Telnet" },    { 25,   "SMTP" },
        { 53,   "DNS" },        { 80,  "HTTP" },      { 88,   "Kerberos" },
        { 135,  "RPC" },        { 139, "NetBIOS" },   { 143,  "IMAP" },
        { 389,  "LDAP" },       { 443, "HTTPS" },     { 445,  "SMB" },
        { 464,  "Kpasswd" },    { 636, "LDAPS" },     { 1433, "MSSQL" },
        { 1521, "Oracle" },     { 3306,"MySQL" },     { 3389, "RDP" },
        { 5985, "WinRM-HTTP" }, { 5986,"WinRM-HTTPS"},{ 8080, "HTTP-Alt" },
        { 8443, "HTTPS-Alt" }
    };

    private static readonly Dictionary<OsType, string[]> OsVersions = new()
    {
        [OsType.Windows] = ["Windows Server 2019", "Windows Server 2022", "Windows 10 Enterprise", "Windows 11 Enterprise"],
        [OsType.Linux]   = ["Ubuntu 22.04 LTS", "CentOS 8", "RHEL 9", "Debian 12"],
        [OsType.MacOS]   = ["macOS Ventura 13.6", "macOS Sonoma 14.2"]
    };

    private static readonly string[] WindowsServices =
        ["Active Directory", "IIS 10.0", "SQL Server 2019", "Exchange Server", "SharePoint", "WSUS", "WMI"];
    private static readonly string[] LinuxServices =
        ["nginx/1.24.0", "Apache/2.4.58", "OpenSSH 9.3", "PostgreSQL 15", "Docker 24.0", "Kubernetes"];

    // MITRE ATT&CK lateral movement techniques
    private static readonly (string Id, string Name, string Description)[] LateralTechniques =
    [
        ("T1021.001", "Remote Desktop Protocol",   "Adversary uses RDP to pivot between systems"),
        ("T1021.002", "SMB/Windows Admin Shares",  "Leveraging SMB ADMIN$ share for lateral movement"),
        ("T1021.006", "Windows Remote Management", "Using WinRM (PSRemoting) for remote execution"),
        ("T1047",     "Windows Management Instr.", "WMI used to execute commands on remote host"),
        ("T1563.001", "SSH Hijacking",             "Hijacking existing SSH connections for lateral movement"),
    ];

    public NetworkSimService(ILogger<NetworkSimService> logger)
    {
        _logger = logger;
    }

    public async Task<NetworkScanResult> SimulateScanAsync(string subnet, CancellationToken ct = default)
    {
        _logger.LogInformation("Simulating network scan on subnet {Subnet}", subnet);
        var startTime = DateTime.UtcNow;
        var hosts     = new List<SimulatedHost>();

        // Generate 8-20 realistic hosts
        var hostCount = Random.Shared.Next(8, 21);

        for (int i = 1; i <= hostCount && !ct.IsCancellationRequested; i++)
        {
            await Task.Delay(Random.Shared.Next(80, 250), ct); // simulate scan time

            var host = GenerateSimulatedHost(subnet, i);
            hosts.Add(host);

            _logger.LogDebug("Discovered: {IP} ({OS}) - {Ports} ports open",
                host.IpAddress, host.OperatingSystem, host.OpenPorts.Count);
        }

        _discoveredHosts.AddRange(hosts);

        return new NetworkScanResult
        {
            Subnet       = subnet,
            Hosts        = hosts,
            ScanDuration = DateTime.UtcNow - startTime
        };
    }

    public async Task<LateralMovementResult> SimulatePivotAsync(string fromHost, string toHost)
    {
        var technique = LateralTechniques[Random.Shared.Next(LateralTechniques.Length)];
        var success   = Random.Shared.NextDouble() > 0.25; // 75% success rate

        await Task.Delay(Random.Shared.Next(500, 1500)); // simulate attempt

        var steps = new List<string>
        {
            $"[*] Enumerating target {toHost} services...",
            $"[*] Identifying authentication mechanism on {toHost}...",
            $"[*] Attempting {technique.Name} ({technique.Id})...",
        };

        if (success)
        {
            steps.Add($"[+] Successfully established session on {toHost} via {technique.Name}");
            steps.Add("[+] Dumping local user enumeration...");
            steps.Add("[+] Checking for cached credentials...");
        }
        else
        {
            steps.Add($"[-] Access denied — authentication failure on {toHost}");
            steps.Add("[-] Falling back to alternate technique...");
        }

        return new LateralMovementResult
        {
            FromHost           = fromHost,
            ToHost             = toHost,
            Success            = success,
            Technique          = technique.Name,
            MitreTechniqueId   = technique.Id,
            StepsLog           = steps
        };
    }

    public Task<IEnumerable<SimulatedHost>> GetDiscoveredHostsAsync()
        => Task.FromResult<IEnumerable<SimulatedHost>>(_discoveredHosts.AsReadOnly());

    // ── Private ───────────────────────────────────────────────────────────────

    private static SimulatedHost GenerateSimulatedHost(string subnet, int hostIndex)
    {
        var baseIp   = subnet.TrimEnd('/', '0').TrimEnd('.', '0');
        var ip       = $"{baseIp}.{Random.Shared.Next(2, 254)}";
        var osType   = Random.Shared.NextDouble() switch
        {
            < 0.60 => OsType.Windows,
            < 0.90 => OsType.Linux,
            _      => OsType.MacOS
        };
        var osVersions = OsVersions[osType];
        var osVersion  = osVersions[Random.Shared.Next(osVersions.Length)];

        var ports     = GetRealisticPorts(osType);
        var services  = osType == OsType.Windows ? WindowsServices : LinuxServices;
        var openSvcs  = services.OrderBy(_ => Random.Shared.Next()).Take(Random.Shared.Next(2, 5)).ToList();

        var hostnames = new[] { "DC01", "FS02", "WEB01", "SQL01", "APP01", "DEV03", "JUMP01", "WKS" };
        var hostname  = hostIndex <= 8
            ? hostnames[hostIndex - 1]
            : $"HOST{hostIndex:D2}";

        return new SimulatedHost
        {
            IpAddress        = ip,
            Hostname         = hostname,
            OperatingSystem  = osType,
            OsVersion        = osVersion,
            OpenPorts        = ports,
            RunningServices  = openSvcs,
            ThreatScore      = ports.Any(p => p.Port is 23 or 445 or 3389)
                                ? ThreatLevel.Medium
                                : ThreatLevel.Low
        };
    }

    private static List<OpenPort> GetRealisticPorts(OsType os)
    {
        var candidates = os switch
        {
            OsType.Windows => new[] { 135, 139, 445, 3389, 88, 389, 5985, 80, 443 },
            OsType.Linux   => new[] { 22, 80, 443, 3306, 5432, 8080, 8443, 53 },
            _              => new[] { 22, 80, 443 }
        };

        return candidates
            .Where(_ => Random.Shared.NextDouble() > 0.35)
            .Select(p => new OpenPort(p, "tcp", CommonPorts.GetValueOrDefault(p, "unknown"), null))
            .ToList();
    }
}
