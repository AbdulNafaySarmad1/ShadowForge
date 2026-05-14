using System.Text;
using ShadowForge.Core.Interfaces;
using ShadowForge.Core.Models;
using Microsoft.Extensions.Logging;

namespace ShadowForge.Services.Reporting;

/// <summary>
/// Generates corporate-grade PDF reports for ShadowForge simulations.
/// Uses an HTML-intermediate approach: builds styled HTML, then converts
/// to PDF via PuppeteerSharp (headless Chromium) for pixel-perfect output.
///
/// HOW IT WORKS:
///   GenerateReportAsync()  â†’ assembles SimulationReport from session data
///   ExportToPdfAsync()     â†’ renders HTML template â†’ PDF bytes via PuppeteerSharp
///   ExportToJsonAsync()    â†’ serialises report to indented JSON
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly ILogger<ReportService> _logger;

    public ReportService(ILogger<ReportService> logger)
    {
        _logger = logger;
    }

    // â”€â”€ IReportService: build the report object â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public Task<SimulationReport> GenerateReportAsync(Guid sessionId)
    {
        // In a real flow this would pull from DB via repositories.
        // The method exists so DashboardStateService can pass its in-memory
        // data through ExportToPdfAsync directly.
        var report = new SimulationReport
        {
            SessionId   = sessionId,
            Title       = $"ShadowForge APT Simulation â€” {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
            GeneratedAt = DateTime.UtcNow,
            Analyst     = "ShadowForge AutoReport"
        };
        return Task.FromResult(report);
    }

    public Task<IEnumerable<MitreAttackMapping>> GetMitreAttackMappingsAsync()
        => Task.FromResult<IEnumerable<MitreAttackMapping>>([]);

    public Task<string> ExportToJsonAsync(SimulationReport report)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(report,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return Task.FromResult(json);
    }

    // â”€â”€ PDF export â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task<byte[]> ExportToPdfAsync(SimulationReport report)
    {
        try
        {
            var html = BuildHtml(report);
            return await RenderHtmlToPdfAsync(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF export failed for session {SessionId}", report.SessionId);
            throw;
        }
    }

    // â”€â”€ HTML template builder â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static string BuildHtml(SimulationReport report)
    {
        var sb = new StringBuilder();

        // â”€â”€ threat level helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        static string ThreatColor(ThreatLevel level) => level switch
        {
            ThreatLevel.Critical => "#ef4444",
            ThreatLevel.High     => "#f97316",
            ThreatLevel.Medium   => "#eab308",
            ThreatLevel.Low      => "#22c55e",
            _                    => "#6b7280"
        };

        static string ThreatBg(ThreatLevel level) => level switch
        {
            ThreatLevel.Critical => "#fef2f2",
            ThreatLevel.High     => "#fff7ed",
            ThreatLevel.Medium   => "#fefce8",
            ThreatLevel.Low      => "#f0fdf4",
            _                    => "#f9fafb"
        };

        static string ThreatBadge(ThreatLevel level)
        {
            var color = level switch
            {
                ThreatLevel.Critical => "#ef4444",
                ThreatLevel.High     => "#f97316",
                ThreatLevel.Medium   => "#eab308",
                ThreatLevel.Low      => "#22c55e",
                _                    => "#6b7280"
            };
            return $@"<span style=""background:{color};color:#fff;padding:2px 8px;border-radius:4px;
                             font-size:11px;font-weight:700;letter-spacing:.5px;
                             text-transform:uppercase"">{level}</span>";
        }

        var overallColor = ThreatColor(report.OverallRiskLevel);

        // â”€â”€ overall stats â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        int totalEvents   = report.ModuleResults.Sum(r => r.Events.Count);
        int criticalCount = report.ModuleResults.SelectMany(r => r.Events).Count(e => e.ThreatLevel == ThreatLevel.Critical);
        int highCount     = report.ModuleResults.SelectMany(r => r.Events).Count(e => e.ThreatLevel == ThreatLevel.High);
        int hostCount     = report.DiscoveredHosts.Count;
        int compromised   = report.DiscoveredHosts.Count(h => h.IsCompromised);
        int iocCount      = report.IOCsIdentified.Count;
        int mitreCount    = report.MitreMappings.Count;

        sb.Append($@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8""/>
<title>{report.Title}</title>
<style>
  * {{ box-sizing: border-box; margin: 0; padding: 0; }}
  body {{
    font-family: 'Segoe UI', Arial, sans-serif;
    background: #fff;
    color: #1e293b;
    font-size: 13px;
    line-height: 1.6;
  }}

  /* â”€â”€ COVER PAGE â”€â”€ */
  .cover {{
    width: 100%;
    min-height: 100vh;
    background: linear-gradient(135deg, #0f172a 0%, #1e293b 60%, #0f172a 100%);
    color: #fff;
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    padding: 60px 64px;
    page-break-after: always;
  }}
  .cover-logo {{
    font-size: 13px;
    letter-spacing: 3px;
    text-transform: uppercase;
    color: #94a3b8;
    font-weight: 600;
  }}
  .cover-logo span {{ color: {overallColor}; }}
  .cover-title {{
    font-size: 42px;
    font-weight: 800;
    line-height: 1.15;
    margin-bottom: 16px;
    letter-spacing: -0.5px;
  }}
  .cover-subtitle {{
    font-size: 16px;
    color: #94a3b8;
    margin-bottom: 32px;
  }}
  .cover-risk-badge {{
    display: inline-flex;
    align-items: center;
    gap: 10px;
    background: rgba(255,255,255,.08);
    border: 1px solid rgba(255,255,255,.12);
    border-left: 4px solid {overallColor};
    padding: 14px 20px;
    border-radius: 8px;
    margin-bottom: 40px;
  }}
  .cover-risk-badge .risk-label {{ font-size: 11px; color: #94a3b8; text-transform:uppercase; letter-spacing:.8px; }}
  .cover-risk-badge .risk-value {{ font-size: 20px; font-weight: 800; color: {overallColor}; }}
  .cover-meta {{
    display: flex;
    gap: 40px;
    font-size: 12px;
    color: #64748b;
  }}
  .cover-meta strong {{ color: #cbd5e1; display: block; }}

  /* â”€â”€ STAT CARDS â”€â”€ */
  .stat-grid {{
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 16px;
    margin: 24px 0;
  }}
  .stat-card {{
    background: #f8fafc;
    border: 1px solid #e2e8f0;
    border-radius: 10px;
    padding: 20px 16px;
    text-align: center;
  }}
  .stat-card .stat-num {{
    font-size: 32px;
    font-weight: 800;
    line-height: 1;
    margin-bottom: 6px;
  }}
  .stat-card .stat-label {{
    font-size: 11px;
    color: #64748b;
    text-transform: uppercase;
    letter-spacing: .6px;
  }}

  /* â”€â”€ CONTENT PAGES â”€â”€ */
  .page {{
    padding: 48px 64px;
    page-break-after: always;
  }}
  .page:last-child {{ page-break-after: auto; }}

  /* â”€â”€ SECTION HEADERS â”€â”€ */
  .section-tag {{
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 2px;
    text-transform: uppercase;
    color: #94a3b8;
    margin-bottom: 4px;
  }}
  h2 {{
    font-size: 24px;
    font-weight: 800;
    color: #0f172a;
    margin-bottom: 20px;
    padding-bottom: 12px;
    border-bottom: 2px solid #f1f5f9;
  }}
  h3 {{
    font-size: 16px;
    font-weight: 700;
    color: #1e293b;
    margin: 24px 0 12px;
  }}

  /* â”€â”€ PANELS â”€â”€ */
  .panel {{
    background: #fff;
    border: 1px solid #e2e8f0;
    border-radius: 10px;
    margin-bottom: 20px;
    overflow: hidden;
  }}
  .panel-header {{
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 14px 20px;
    background: #f8fafc;
    border-bottom: 1px solid #e2e8f0;
    font-weight: 700;
    font-size: 13px;
  }}
  .panel-body {{ padding: 20px; }}

  /* â”€â”€ TABLES â”€â”€ */
  table {{
    width: 100%;
    border-collapse: collapse;
    font-size: 12px;
  }}
  thead tr {{
    background: #0f172a;
    color: #e2e8f0;
  }}
  thead th {{
    padding: 10px 14px;
    text-align: left;
    font-weight: 600;
    font-size: 11px;
    letter-spacing: .5px;
    text-transform: uppercase;
  }}
  tbody tr {{ border-bottom: 1px solid #f1f5f9; }}
  tbody tr:last-child {{ border-bottom: none; }}
  tbody tr:hover {{ background: #f8fafc; }}
  tbody td {{ padding: 10px 14px; vertical-align: middle; }}

  /* â”€â”€ EVENTS â”€â”€ */
  .event-row {{
    display: flex;
    align-items: flex-start;
    gap: 12px;
    padding: 10px 0;
    border-bottom: 1px solid #f1f5f9;
  }}
  .event-row:last-child {{ border-bottom: none; }}
  .event-type {{
    font-size: 11px;
    color: #64748b;
    min-width: 140px;
    padding-top: 2px;
  }}
  .event-desc {{ flex: 1; color: #334155; }}
  .event-mitre {{
    font-size: 10px;
    font-weight: 700;
    color: #6366f1;
    background: #eef2ff;
    padding: 2px 6px;
    border-radius: 4px;
    white-space: nowrap;
  }}
  .event-ts {{
    font-size: 10px;
    color: #94a3b8;
    white-space: nowrap;
  }}

  /* â”€â”€ MODULE RESULT HEADER â”€â”€ */
  .module-header {{
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: #0f172a;
    color: #e2e8f0;
    padding: 14px 20px;
    border-radius: 10px 10px 0 0;
  }}
  .module-header .mod-name {{ font-weight: 800; font-size: 15px; }}
  .module-meta {{
    display: flex;
    gap: 20px;
    padding: 12px 20px;
    background: #f8fafc;
    font-size: 12px;
    color: #475569;
    border-bottom: 1px solid #e2e8f0;
  }}
  .module-meta strong {{ color: #0f172a; }}

  /* â”€â”€ FOOTER â”€â”€ */
  .report-footer {{
    margin-top: 40px;
    padding-top: 20px;
    border-top: 1px solid #e2e8f0;
    display: flex;
    justify-content: space-between;
    font-size: 11px;
    color: #94a3b8;
  }}

  /* â”€â”€ PRINT / PAGE â”€â”€ */
  @media print {{
    body {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
  }}
  @page {{ margin: 0; size: A4; }}
</style>
</head>
<body>
");

        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        // COVER PAGE
        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        sb.Append($@"
<div class=""cover"">
  <div class=""cover-logo"">Shadow<span>Forge</span> Â· APT Emulation Framework</div>
  <div>
    <div class=""cover-title"">Simulation<br/>Report</div>
    <div class=""cover-subtitle"">{report.Title}</div>
    <div class=""cover-risk-badge"">
      <div>
        <div class=""risk-label"">Overall Risk Level</div>
        <div class=""risk-value"">{report.OverallRiskLevel}</div>
      </div>
    </div>
    <div class=""stat-grid"" style=""grid-template-columns:repeat(4,1fr);gap:12px"">
      <div class=""stat-card"" style=""background:rgba(255,255,255,.06);border-color:rgba(255,255,255,.1)"">
        <div class=""stat-num"" style=""color:#ef4444"">{criticalCount}</div>
        <div class=""stat-label"" style=""color:#94a3b8"">Critical</div>
      </div>
      <div class=""stat-card"" style=""background:rgba(255,255,255,.06);border-color:rgba(255,255,255,.1)"">
        <div class=""stat-num"" style=""color:#f97316"">{highCount}</div>
        <div class=""stat-label"" style=""color:#94a3b8"">High</div>
      </div>
      <div class=""stat-card"" style=""background:rgba(255,255,255,.06);border-color:rgba(255,255,255,.1)"">
        <div class=""stat-num"" style=""color:#e2e8f0"">{hostCount}</div>
        <div class=""stat-label"" style=""color:#94a3b8"">Hosts Found</div>
      </div>
      <div class=""stat-card"" style=""background:rgba(255,255,255,.06);border-color:rgba(255,255,255,.1)"">
        <div class=""stat-num"" style=""color:#e2e8f0"">{totalEvents}</div>
        <div class=""stat-label"" style=""color:#94a3b8"">Total Events</div>
      </div>
    </div>
  </div>
  <div class=""cover-meta"">
    <div><strong>Session ID</strong>{report.SessionId}</div>
    <div><strong>Generated</strong>{report.GeneratedAt:yyyy-MM-dd HH:mm} UTC</div>
    <div><strong>Analyst</strong>{report.Analyst}</div>
    <div><strong>Classification</strong>CONFIDENTIAL</div>
  </div>
</div>
");

        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        // PAGE 2 â€” EXECUTIVE SUMMARY
        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        sb.Append($@"
<div class=""page"">
  <div class=""section-tag"">Section 01</div>
  <h2>Executive Summary</h2>
  <div class=""stat-grid"">
    <div class=""stat-card"">
      <div class=""stat-num"">{totalEvents}</div>
      <div class=""stat-label"">Total Events</div>
    </div>
    <div class=""stat-card"">
      <div class=""stat-num"" style=""color:#ef4444"">{criticalCount}</div>
      <div class=""stat-label"">Critical Severity</div>
    </div>
    <div class=""stat-card"">
      <div class=""stat-num"" style=""color:#f97316"">{highCount}</div>
      <div class=""stat-label"">High Severity</div>
    </div>
    <div class=""stat-card"">
      <div class=""stat-num"">{mitreCount}</div>
      <div class=""stat-label"">MITRE Techniques</div>
    </div>
    <div class=""stat-card"">
      <div class=""stat-num"">{hostCount}</div>
      <div class=""stat-label"">Hosts Discovered</div>
    </div>
    <div class=""stat-card"">
      <div class=""stat-num"" style=""color:#f97316"">{compromised}</div>
      <div class=""stat-label"">Hosts Compromised</div>
    </div>
    <div class=""stat-card"">
      <div class=""stat-num"">{iocCount}</div>
      <div class=""stat-label"">IOCs Identified</div>
    </div>
    <div class=""stat-card"">
      <div class=""stat-num"" style=""color:{overallColor}"">{report.OverallRiskLevel}</div>
      <div class=""stat-label"">Overall Risk</div>
    </div>
  </div>
");

        if (!string.IsNullOrWhiteSpace(report.Summary.Overview))
        {
            sb.Append($@"
  <div class=""panel"">
    <div class=""panel-header"">Overview</div>
    <div class=""panel-body""><p>{report.Summary.Overview}</p></div>
  </div>");
        }

        if (report.Summary.KeyTakeaways.Any())
        {
            sb.Append(@"<div class=""panel""><div class=""panel-header"">Key Takeaways</div><div class=""panel-body""><ul style=""padding-left:18px;line-height:2"">");
            foreach (var t in report.Summary.KeyTakeaways)
                sb.Append($"<li>{t}</li>");
            sb.Append("</ul></div></div>");
        }

        if (report.Summary.Recommendations.Any())
        {
            sb.Append(@"<div class=""panel""><div class=""panel-header"">Recommendations</div><div class=""panel-body""><ol style=""padding-left:18px;line-height:2"">");
            foreach (var r in report.Summary.Recommendations)
                sb.Append($"<li>{r}</li>");
            sb.Append("</ol></div></div>");
        }

        sb.Append("</div>"); // end page

        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        // PAGE(S) â€” MODULE RESULTS
        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        if (report.ModuleResults.Any())
        {
            sb.Append(@"<div class=""page""><div class=""section-tag"">Section 02</div><h2>Module Results</h2>");

            foreach (var mod in report.ModuleResults)
            {
                var statusColor = mod.Status == ModuleStatus.Completed ? "#22c55e" : "#ef4444";
                sb.Append($@"
  <div class=""panel"" style=""margin-bottom:24px"">
    <div class=""module-header"">
      <span class=""mod-name"">{mod.ModuleName}</span>
      <span style=""background:{statusColor};color:#fff;padding:3px 10px;border-radius:4px;font-size:11px;font-weight:700"">{mod.Status}</span>
    </div>
    <div class=""module-meta"">
      <span>Duration: <strong>{mod.Duration.TotalSeconds:F2}s</strong></span>
      <span>Events: <strong>{mod.Events.Count}</strong></span>
      <span>Peak Threat: <strong style=""color:{ThreatColor(mod.HighestThreatLevel)}"">{mod.HighestThreatLevel}</strong></span>
      <span>Started: <strong>{mod.StartedAt:HH:mm:ss} UTC</strong></span>
    </div>");

                if (mod.Events.Any())
                {
                    sb.Append(@"<div class=""panel-body"">");
                    foreach (var evt in mod.Events.OrderByDescending(e => e.ThreatLevel).Take(15))
                    {
                        sb.Append($@"
      <div class=""event-row"" style=""border-left:3px solid {ThreatColor(evt.ThreatLevel)};padding-left:10px"">
        <div>{ThreatBadge(evt.ThreatLevel)}</div>
        <div class=""event-type"">{evt.Type}</div>
        <div class=""event-desc"">{evt.Description}</div>
        {(evt.MitreTechniqueId is not null ? $@"<div class=""event-mitre"">{evt.MitreTechniqueId}</div>" : "")}
        <div class=""event-ts"">{evt.Timestamp:HH:mm:ss}</div>
      </div>");
                    }
                    if (mod.Events.Count > 15)
                        sb.Append($@"<p style=""font-size:11px;color:#94a3b8;margin-top:10px"">â€¦ and {mod.Events.Count - 15} more events not shown.</p>");
                    sb.Append("</div>");
                }

                sb.Append("</div>"); // end panel
            }

            sb.Append("</div>"); // end page
        }

        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        // PAGE â€” DISCOVERED HOSTS
        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        if (report.DiscoveredHosts.Any())
        {
            sb.Append(@"<div class=""page""><div class=""section-tag"">Section 03</div><h2>Discovered Hosts</h2>
  <div class=""panel"">
    <table>
      <thead><tr><th>IP Address</th><th>Hostname</th><th>OS</th><th>Open Ports</th><th>Threat Score</th><th>Compromised</th></tr></thead>
      <tbody>");
            foreach (var host in report.DiscoveredHosts)
            {
                var compCell = host.IsCompromised
                    ? @"<span style=""color:#ef4444;font-weight:700"">YES</span>"
                    : @"<span style=""color:#22c55e"">No</span>";
                sb.Append($@"
        <tr>
          <td style=""font-family:monospace;font-weight:600"">{host.IpAddress}</td>
          <td>{host.Hostname}</td>
          <td>{host.OsVersion}</td>
          <td>{host.OpenPorts.Count}</td>
          <td>{ThreatBadge(host.ThreatScore)}</td>
          <td>{compCell}</td>
        </tr>");
            }
            sb.Append("</tbody></table></div></div>");
        }

        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        // PAGE â€” MITRE ATT&CK MAPPINGS
        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        if (report.MitreMappings.Any())
        {
            sb.Append(@"<div class=""page""><div class=""section-tag"">Section 04</div><h2>MITRE ATT&CK Mappings</h2>
  <div class=""panel"">
    <table>
      <thead><tr><th>Technique ID</th><th>Name</th><th>Tactic</th><th>Severity</th><th>Times Observed</th></tr></thead>
      <tbody>");
            foreach (var m in report.MitreMappings.OrderByDescending(x => x.TimesObserved))
            {
                sb.Append($@"
        <tr>
          <td><span style=""color:#6366f1;font-weight:700;font-family:monospace"">{m.TechniqueId}</span></td>
          <td>{m.TechniqueName}</td>
          <td>{m.Tactic}</td>
          <td>{ThreatBadge(m.Severity)}</td>
          <td style=""text-align:center;font-weight:700"">{m.TimesObserved}</td>
        </tr>");
            }
            sb.Append("</tbody></table></div></div>");
        }

        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        // PAGE â€” IOCs IDENTIFIED
        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        if (report.IOCsIdentified.Any())
        {
            sb.Append(@"<div class=""page""><div class=""section-tag"">Section 05</div><h2>IOCs Identified</h2>
  <div class=""panel"">
    <table>
      <thead><tr><th>ID</th><th>Name</th><th>Description</th><th>Severity</th><th>Indicators</th></tr></thead>
      <tbody>");
            foreach (var ioc in report.IOCsIdentified.OrderByDescending(i => i.ThreatLevel))
            {
                sb.Append($@"
        <tr>
          <td style=""font-family:monospace;font-size:11px;color:#6366f1"">{ioc.Id}</td>
          <td style=""font-weight:600"">{ioc.Name}</td>
          <td style=""color:#475569"">{ioc.Description}</td>
          <td>{ThreatBadge(ioc.ThreatLevel)}</td>
          <td style=""text-align:center"">{ioc.IndicatorCount}</td>
        </tr>");
            }
            sb.Append("</tbody></table></div></div>");
        }

        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        // REPORT FOOTER
        // â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
        sb.Append($@"
<div class=""page"" style=""page-break-after:auto"">
  <div class=""report-footer"">
    <span>ShadowForge APT Emulation Framework â€” CONFIDENTIAL</span>
    <span>Session: {report.SessionId}</span>
    <span>Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC</span>
  </div>
</div>

</body></html>");

        return sb.ToString();
    }

    // â”€â”€ PuppeteerSharp PDF renderer â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

           private async Task<byte[]> RenderHtmlToPdfAsync(string html)
{
    var fetcher = new PuppeteerSharp.BrowserFetcher();
    var installed = fetcher.GetInstalledBrowsers();
    if (!installed.Any())
        await fetcher.DownloadAsync();

    await using var browser = await PuppeteerSharp.Puppeteer.LaunchAsync(new PuppeteerSharp.LaunchOptions
    {
        Headless = true,
        Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
    });

    await using var page = await browser.NewPageAsync();
    await page.SetContentAsync(html, new PuppeteerSharp.NavigationOptions
    {
        WaitUntil = new[] { PuppeteerSharp.WaitUntilNavigation.Load }
    });

    return await page.PdfDataAsync(new PuppeteerSharp.PdfOptions
    {
        Format = PuppeteerSharp.Media.PaperFormat.A4,
        PrintBackground = true,
        MarginOptions = new PuppeteerSharp.Media.MarginOptions { Top = "0mm", Bottom = "0mm", Left = "0mm", Right = "0mm" }
    });
}
}