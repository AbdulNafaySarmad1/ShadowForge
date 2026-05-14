using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ShadowForge.Core.Interfaces;
using ShadowForge.Core.Models;

namespace ShadowForge.Services.ThreatIntel;

/// <summary>
/// Pulls LIVE threat intelligence from AlienVault OTX and AbuseIPDB.
/// Demonstrates: HttpClientFactory, async/await, exception handling, JSON deserialization.
/// </summary>
public sealed class ThreatIntelService : IThreatIntelService, IDisposable
{
    private readonly HttpClient _otxClient;
    private readonly HttpClient _abuseClient;
    private readonly ILogger<ThreatIntelService> _logger;

    // Simple in-process cache to avoid hammering free-tier APIs
    private readonly Dictionary<string, (IpReputation Data, DateTime CachedAt)> _ipCache = [];
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(10);

    public ThreatIntelService(
        IHttpClientFactory httpClientFactory,
        ILogger<ThreatIntelService> logger)
    {
        _otxClient   = httpClientFactory.CreateClient("OTX");
        _abuseClient = httpClientFactory.CreateClient("AbuseIPDB");
        _logger      = logger;
    }

    // â”€â”€ IP Reputation (combines OTX + AbuseIPDB) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task<IpReputation> CheckIpReputationAsync(string ipAddress)
    {
        // Return cached if fresh
        if (_ipCache.TryGetValue(ipAddress, out var cached) &&
            DateTime.UtcNow - cached.CachedAt < _cacheTtl)
        {
            _logger.LogDebug("IP {IP} served from cache.", ipAddress);
            return cached.Data;
        }

        var reputation = new IpReputation { IpAddress = ipAddress };

        // Parallel requests to both APIs
        var otxTask   = FetchOtxIpAnalysisAsync(ipAddress);
        var abuseTask = FetchAbuseIpDbAsync(ipAddress);

        await Task.WhenAll(otxTask, abuseTask);

        // Merge results
        if (otxTask.Result is { } otx)
        {
            reputation.AssociatedCampaigns = otx.Campaigns ?? [];
            reputation.Tags = otx.Tags ?? [];
        }

        if (abuseTask.Result is { } abuse)
        {
            reputation.AbuseScore         = abuse.AbuseConfidenceScore;
            reputation.CountryCode        = abuse.CountryCode;
            reputation.Isp                = abuse.Isp;
            reputation.IsPubliclyRoutable = abuse.IsPublic;
        }

        reputation.ThreatLevel = reputation.AbuseScore switch
        {
            >= 80 => ThreatLevel.Critical,
            >= 50 => ThreatLevel.High,
            >= 20 => ThreatLevel.Medium,
            >= 5  => ThreatLevel.Low,
            _     => ThreatLevel.None
        };

        _ipCache[ipAddress] = (reputation, DateTime.UtcNow);
        return reputation;
    }

    // â”€â”€ Latest OTX pulses (live IOC feed) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task<IEnumerable<ThreatIndicator>> GetLatestPulsesAsync(int limit = 20)
    {
        try
        {
            var response = await _otxClient.GetFromJsonAsync<OtxPulseSubscriptionResponse>(
                $"api/v1/pulses/subscribed?limit={limit}");

            if (response?.Results is null) return [];

            return response.Results.Select(p => new ThreatIndicator
            {
                Id             = p.Id,
                Name           = p.Name,
                Description    = p.Description ?? string.Empty,
                Author         = p.Author?.Username ?? "unknown",
                Modified       = p.Modified,
                Tags           = p.Tags ?? [],
                IndicatorCount = p.IndicatorCount,
                ThreatLevel    = MapPulseToThreatLevel(p.Tlp, p.Name, p.Tags)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch OTX pulses.");
            return GetFallbackIndicators(); // demo data if API fails
        }
    }

    // â”€â”€ Domain analysis â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task<DomainIntel> AnalyzeDomainAsync(string domain)
    {
        try
        {
            var response = await _otxClient.GetFromJsonAsync<OtxDomainResponse>(
                $"api/v1/indicators/domain/{domain}/general");

            if (response is null) return new DomainIntel { Domain = domain };

            return new DomainIntel
            {
                Domain          = domain,
                IsMalicious     = response.PulseInfo?.Count > 0,
                ThreatLevel     = response.PulseInfo?.Count > 5 ? ThreatLevel.High
                                : response.PulseInfo?.Count > 0 ? ThreatLevel.Medium
                                : ThreatLevel.None,
                AssociatedIps   = response.Geo?.CountryName is { } c ? [c] : []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze domain {Domain}.", domain);
            return new DomainIntel { Domain = domain };
        }
    }

    // â”€â”€ File hash lookup â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task<FileHashIntel> CheckFileHashAsync(string hash)
    {
        try
        {
            var response = await _otxClient.GetFromJsonAsync<OtxHashResponse>(
                $"api/v1/indicators/file/{hash}/analysis");

            return new FileHashIntel
            {
                Hash           = hash,
                IsMalicious    = response?.Analysis?.Plugins?.Any() == true,
                ThreatLevel    = response?.Analysis?.Plugins?.Any() == true ? ThreatLevel.High : ThreatLevel.None
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check file hash {Hash}.", hash);
            return new FileHashIntel { Hash = hash };
        }
    }

    // â”€â”€ Private helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task<OtxIpResult?> FetchOtxIpAnalysisAsync(string ip)
    {
        try
        {
            return await _otxClient.GetFromJsonAsync<OtxIpResult>(
                $"api/v1/indicators/IPv4/{ip}/general");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OTX lookup failed for {IP}.", ip);
            return null;
        }
    }

    private async Task<AbuseIpDbResult?> FetchAbuseIpDbAsync(string ip)
    {
        try
        {
            var response = await _abuseClient.GetFromJsonAsync<AbuseIpDbResponse>(
                $"api/v2/check?ipAddress={ip}&maxAgeInDays=90&verbose");
            return response?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AbuseIPDB lookup failed for {IP}.", ip);
            return null;
        }
    }

            private static ThreatLevel MapPulseToThreatLevel(string? tlp, string name, List<string> tags)
    {
        // TLP red/amber = high confidence severity
        if (tlp?.ToLower() == "red")   return ThreatLevel.Critical;
        if (tlp?.ToLower() == "amber") return ThreatLevel.High;

        // Keyword-based scoring on name + tags
        var text = (name + " " + string.Join(" ", tags)).ToLower();
        var criticalKeywords = new[] { "apt", "apt3", "apt37", "apt41", "seedworm", "lazarus", "kimsuky", "ransomware", "backdoor", "implant", "rat ", "trojan", "rootkit", "zero-day", "0-day", "0day", "0day", "critical", "rce", "exploit" };
        var highKeywords     = new[] { "malware", "trojan", "botnet", "c2", "phishing", "rat ", "stealer", "dropper", "loader" };
        var mediumKeywords   = new[] { "scan", "brute", "recon", "spray", "fraud", "spam", "miner" };

        if (criticalKeywords.Any(k => text.Contains(k))) return ThreatLevel.Critical;
        if (highKeywords.Any(k => text.Contains(k)))     return ThreatLevel.High;
        if (mediumKeywords.Any(k => text.Contains(k)))   return ThreatLevel.Medium;
        if (tlp?.ToLower() == "green")                   return ThreatLevel.Medium;

        return ThreatLevel.Low;
    }

    // Fallback demo data so the dashboard never shows empty even with no API key
    private static List<ThreatIndicator> GetFallbackIndicators() =>
    [
        new() { Id = "demo-1", Name = "APT29 Cozy Bear Campaign",        Description = "Russian state-sponsored phishing campaign",   ThreatLevel = ThreatLevel.Critical, IndicatorCount = 142, Tags = ["apt29","russia","phishing"] },
        new() { Id = "demo-2", Name = "Lazarus Group Malware",           Description = "DPRK financially motivated threat actor",     ThreatLevel = ThreatLevel.High,     IndicatorCount = 87,  Tags = ["lazarus","dprk","banking"] },
        new() { Id = "demo-3", Name = "Emotet Botnet Infrastructure",    Description = "Known Emotet C2 servers and drop zones",      ThreatLevel = ThreatLevel.High,     IndicatorCount = 231, Tags = ["emotet","botnet","c2"] },
        new() { Id = "demo-4", Name = "RansomHub Affiliate Activity",    Description = "RaaS affiliate IOCs observed in enterprise",  ThreatLevel = ThreatLevel.Critical, IndicatorCount = 56,  Tags = ["ransomware","raas"] },
        new() { Id = "demo-5", Name = "TA505 Financial Sector Targeting", Description = "FIN7 adjacent group targeting SWIFT networks", ThreatLevel = ThreatLevel.High,     IndicatorCount = 64,  Tags = ["ta505","fin7","financial"] },
    ];

    public void Dispose()
    {
        _otxClient.Dispose();
        _abuseClient.Dispose();
    }

    // â”€â”€ Private DTOs for JSON deserialization â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private record OtxIpResult(
        [property: JsonPropertyName("pulse_info")] OtxPulseInfo? PulseInfo,
        [property: JsonPropertyName("country_name")] string? CountryName,
        List<string>? Tags = null,
        List<string>? Campaigns = null
    );

    private record OtxPulseInfo([property: JsonPropertyName("count")] int Count);

    private record OtxPulseSubscriptionResponse(
        [property: JsonPropertyName("results")] List<OtxPulse>? Results
    );

    private record OtxPulse(
        [property: JsonPropertyName("id")]              string Id,
        [property: JsonPropertyName("name")]            string Name,
        [property: JsonPropertyName("description")]     string? Description,
        [property: JsonPropertyName("modified")]        DateTime Modified,
        [property: JsonPropertyName("author")]          OtxAuthor? Author,
        [property: JsonPropertyName("tags")]            List<string>? Tags,
        [property: JsonPropertyName("indicator_count")] int IndicatorCount,
        [property: JsonPropertyName("tlp")]             string? Tlp
    );

    private record OtxAuthor([property: JsonPropertyName("username")] string Username);

    private record OtxDomainResponse(
        [property: JsonPropertyName("pulse_info")] OtxPulseInfo? PulseInfo,
        [property: JsonPropertyName("geo")]        OtxGeo? Geo
    );

    private record OtxGeo([property: JsonPropertyName("country_name")] string? CountryName);

    private record OtxHashResponse(
        [property: JsonPropertyName("analysis")] OtxHashAnalysis? Analysis
    );

    private record OtxHashAnalysis(
        [property: JsonPropertyName("plugins")] Dictionary<string, object>? Plugins
    );

    private record AbuseIpDbResponse([property: JsonPropertyName("data")] AbuseIpDbResult? Data);

    private record AbuseIpDbResult(
        [property: JsonPropertyName("abuseConfidenceScore")] int AbuseConfidenceScore,
        [property: JsonPropertyName("countryCode")]          string? CountryCode,
        [property: JsonPropertyName("isp")]                  string? Isp,
        [property: JsonPropertyName("isPublic")]             bool IsPublic
    );
}

