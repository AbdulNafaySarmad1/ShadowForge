using ClosedXML.Excel;
using ShadowForge.Core.Models;

namespace ShadowForge.Services.Reporting;

public class XlsxExportService
{
    public byte[] ExportSimulationData(
        Guid sessionId,
        List<SimulationEvent> events,
        List<MitreAttackMapping> mitre,
        List<SimulatedHost> hosts,
        List<ThreatIndicator> iocs)
    {
        using var wb = new XLWorkbook();

        // ── Sheet 1: Summary ─────────────────────────────────────────────
        var ws = wb.AddWorksheet("Summary");
        StyleHeader(ws, "ShadowForge Simulation Report", 1, 6);
        ws.Cell(2, 1).Value = "Session ID";      ws.Cell(2, 2).Value = sessionId.ToString();
        ws.Cell(3, 1).Value = "Generated At";    ws.Cell(3, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
        ws.Cell(4, 1).Value = "Total Events";    ws.Cell(4, 2).Value = events.Count;
        ws.Cell(5, 1).Value = "Critical";        ws.Cell(5, 2).Value = events.Count(e => e.ThreatLevel == ThreatLevel.Critical);
        ws.Cell(6, 1).Value = "High";            ws.Cell(6, 2).Value = events.Count(e => e.ThreatLevel == ThreatLevel.High);
        ws.Cell(7, 1).Value = "Medium";          ws.Cell(7, 2).Value = events.Count(e => e.ThreatLevel == ThreatLevel.Medium);
        ws.Cell(8, 1).Value = "Low";             ws.Cell(8, 2).Value = events.Count(e => e.ThreatLevel == ThreatLevel.Low);
        ws.Cell(9, 1).Value = "Hosts Discovered"; ws.Cell(9, 2).Value = hosts.Count;
        ws.Cell(10, 1).Value = "Hosts Compromised"; ws.Cell(10, 2).Value = hosts.Count(h => h.IsCompromised);
        ws.Cell(11, 1).Value = "MITRE Techniques"; ws.Cell(11, 2).Value = mitre.Count;
        ws.Cell(12, 1).Value = "IOC Pulses";     ws.Cell(12, 2).Value = iocs.Count;
        ws.Column(1).Width = 22; ws.Column(2).Width = 40;
        StyleLabelColumn(ws, 2, 12, 1);

        // ── Sheet 2: Events ───────────────────────────────────────────────
        var wsE = wb.AddWorksheet("Events");
        var eHeaders = new[] { "Timestamp", "Type", "Severity", "Module", "Target", "Description", "MITRE ID", "MITRE Name" };
        WriteHeaders(wsE, eHeaders);
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i]; int r = i + 2;
            wsE.Cell(r, 1).Value = e.Timestamp.ToString("HH:mm:ss");
            wsE.Cell(r, 2).Value = e.Type.ToString();
            wsE.Cell(r, 3).Value = e.ThreatLevel.ToString();
            wsE.Cell(r, 4).Value = e.Source;
            wsE.Cell(r, 5).Value = e.Target;
            wsE.Cell(r, 6).Value = e.Description;
            wsE.Cell(r, 7).Value = e.MitreTechniqueId ?? "";
            wsE.Cell(r, 8).Value = e.MitreTechniqueName ?? "";
            ColorSeverityCell(wsE.Cell(r, 3), e.ThreatLevel);
        }
        AutoFit(wsE, eHeaders.Length);

        // ── Sheet 3: MITRE ATT&CK ─────────────────────────────────────────
        var wsM = wb.AddWorksheet("MITRE ATT&CK");
        var mHeaders = new[] { "Technique ID", "Technique Name", "Tactic", "Severity", "Times Observed", "URL" };
        WriteHeaders(wsM, mHeaders);
        for (int i = 0; i < mitre.Count; i++)
        {
            var m = mitre[i]; int r = i + 2;
            wsM.Cell(r, 1).Value = m.TechniqueId;
            wsM.Cell(r, 2).Value = m.TechniqueName;
            wsM.Cell(r, 3).Value = m.Tactic;
            wsM.Cell(r, 4).Value = m.Severity.ToString();
            wsM.Cell(r, 5).Value = m.TimesObserved;
            wsM.Cell(r, 6).Value = m.Url;
            ColorSeverityCell(wsM.Cell(r, 4), m.Severity);
        }
        AutoFit(wsM, mHeaders.Length);

        // ── Sheet 4: Hosts ────────────────────────────────────────────────
        var wsH = wb.AddWorksheet("Hosts");
        var hHeaders = new[] { "IP Address", "Hostname", "OS", "Open Ports", "Threat Score", "Compromised", "Discovered At" };
        WriteHeaders(wsH, hHeaders);
        for (int i = 0; i < hosts.Count; i++)
        {
            var h = hosts[i]; int r = i + 2;
            wsH.Cell(r, 1).Value = h.IpAddress;
            wsH.Cell(r, 2).Value = h.Hostname;
            wsH.Cell(r, 3).Value = h.OsVersion;
            wsH.Cell(r, 4).Value = h.OpenPorts.Count;
            wsH.Cell(r, 5).Value = h.ThreatScore.ToString();
            wsH.Cell(r, 6).Value = h.IsCompromised ? "YES" : "No";
            wsH.Cell(r, 7).Value = h.DiscoveredAt.ToString("HH:mm:ss");
            ColorSeverityCell(wsH.Cell(r, 5), h.ThreatScore);
            if (h.IsCompromised)
                wsH.Cell(r, 6).Style.Font.FontColor = XLColor.Red;
        }
        AutoFit(wsH, hHeaders.Length);

        // ── Sheet 5: IOCs ─────────────────────────────────────────────────
        var wsI = wb.AddWorksheet("IOC Pulses");
        var iHeaders = new[] { "ID", "Name", "Description", "Severity", "Indicators", "Tags" };
        WriteHeaders(wsI, iHeaders);
        for (int i = 0; i < iocs.Count; i++)
        {
            var ioc = iocs[i]; int r = i + 2;
            wsI.Cell(r, 1).Value = ioc.Id;
            wsI.Cell(r, 2).Value = ioc.Name;
            wsI.Cell(r, 3).Value = ioc.Description;
            wsI.Cell(r, 4).Value = ioc.ThreatLevel.ToString();
            wsI.Cell(r, 5).Value = ioc.IndicatorCount;
            wsI.Cell(r, 6).Value = string.Join(", ", ioc.Tags);
            ColorSeverityCell(wsI.Cell(r, 4), ioc.ThreatLevel);
        }
        AutoFit(wsI, iHeaders.Length);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void WriteHeaders(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static void StyleHeader(IXLWorksheet ws, string title, int row, int mergeTo)
    {
        ws.Cell(row, 1).Value = title;
        ws.Range(row, 1, row, mergeTo).Merge();
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0f172a");
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#6366f1");
        ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void StyleLabelColumn(IXLWorksheet ws, int fromRow, int toRow, int col)
    {
        for (int r = fromRow; r <= toRow; r++)
        {
            ws.Cell(r, col).Style.Font.Bold = true;
            ws.Cell(r, col).Style.Font.FontColor = XLColor.FromHtml("#94a3b8");
        }
    }

    private static void ColorSeverityCell(IXLCell cell, ThreatLevel level)
    {
        cell.Style.Font.FontColor = level switch
        {
            ThreatLevel.Critical => XLColor.FromHtml("#ef4444"),
            ThreatLevel.High     => XLColor.FromHtml("#f97316"),
            ThreatLevel.Medium   => XLColor.FromHtml("#eab308"),
            ThreatLevel.Low      => XLColor.FromHtml("#22c55e"),
            _                    => XLColor.FromHtml("#64748b")
        };
        cell.Style.Font.Bold = level >= ThreatLevel.High;
    }

    private static void AutoFit(IXLWorksheet ws, int colCount)
    {
        for (int i = 1; i <= colCount; i++)
            ws.Column(i).AdjustToContents();
    }
}
