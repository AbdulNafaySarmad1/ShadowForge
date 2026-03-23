using ShadowForge.Core.Models;

namespace ShadowForge.Core.Interfaces;

/// <summary>
/// Generic repository interface - demonstrates generics + encapsulation.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate);
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<int> CountAsync();
}

/// <summary>
/// Threat intelligence API service contract.
/// </summary>
public interface IThreatIntelService
{
    Task<IpReputation> CheckIpReputationAsync(string ipAddress);
    Task<IEnumerable<ThreatIndicator>> GetLatestPulsesAsync(int limit = 20);
    Task<DomainIntel> AnalyzeDomainAsync(string domain);
    Task<FileHashIntel> CheckFileHashAsync(string hash);
}

/// <summary>
/// Fake enterprise user generation service.
/// </summary>
public interface IUserGenService
{
    Task<IEnumerable<FakeUser>> GenerateUsersAsync(int count = 50);
    Task<FakeUser> GenerateSingleUserAsync();
    FakeUser GenerateFromTemplate(UserTemplate template);
}

/// <summary>
/// Network simulation service.
/// </summary>
public interface INetworkSimService
{
    Task<NetworkScanResult> SimulateScanAsync(string subnet, CancellationToken ct = default);
    Task<LateralMovementResult> SimulatePivotAsync(string fromHost, string toHost);
    Task<IEnumerable<SimulatedHost>> GetDiscoveredHostsAsync();
}

/// <summary>
/// Reporting and export service.
/// </summary>
public interface IReportService
{
    Task<SimulationReport> GenerateReportAsync(Guid sessionId);
    Task<byte[]> ExportToPdfAsync(SimulationReport report);
    Task<string> ExportToJsonAsync(SimulationReport report);
    Task<IEnumerable<MitreAttackMapping>> GetMitreAttackMappingsAsync();
}
