# ============================================================
# ShadowForge - Deploy Fixes
# Run from: the root of your shadowforge repo
# Usage:  .\deploy_fixes.ps1
# Usage (custom path):  .\deploy_fixes.ps1 -RepoRoot "C:\path\to\shadowforge"
# ============================================================

param(
    [string]$RepoRoot = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "  ShadowForge Fix Deployer" -ForegroundColor Cyan
Write-Host "  ========================" -ForegroundColor Cyan
Write-Host "  Repo root: $RepoRoot" -ForegroundColor Gray
Write-Host ""

# -- helper -------------------------------------------------------------------
function Copy-Fix {
    param([string]$Src, [string]$Dst)
    $dir = Split-Path $Dst -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Copy-Item -Path $Src -Destination $Dst -Force
    Write-Host "  [OK] $Dst" -ForegroundColor Green
}

$fixes = $PSScriptRoot   # folder where this script lives (same as the fix files)

# -- 1. ThreatIntel.razor (severity filter) ------------------------------------
Copy-Fix `
    "$fixes\ThreatIntel.razor" `
    "$RepoRoot\ShadowForge.Web\Components\Pages\ThreatIntel.razor"

# -- 2. Reports.razor (PDF export button) -------------------------------------
Copy-Fix `
    "$fixes\Reports.razor" `
    "$RepoRoot\ShadowForge.Web\Components\Pages\Reports.razor"

# -- 3. ReportService.cs -------------------------------------------------------
$reportingSvcDir = "$RepoRoot\ShadowForge.Services\Reporting"
Copy-Fix `
    "$fixes\ReportService.cs" `
    "$reportingSvcDir\ReportService.cs"

# -- 4. CSS additions -> append to app.css -------------------------------------
$appCss     = "$RepoRoot\ShadowForge.Web\wwwroot\app.css"
$cssAddons  = Get-Content "$fixes\app_additions.css" -Raw
$existingCss = Get-Content $appCss -Raw

$marker = "/* sf-filter-row */"
if ($existingCss -notlike "*$marker*") {
    Add-Content -Path $appCss -Value "`n$cssAddons"
    Write-Host "  [OK] app.css - filter styles appended" -ForegroundColor Green
} else {
    Write-Host "  [SKIP] app.css - filter styles already present" -ForegroundColor Yellow
}

# -- 5. Register IReportService + ReportService in Program.cs -----------------
$programCs    = "$RepoRoot\ShadowForge.Web\Program.cs"
$programText  = Get-Content $programCs -Raw

$registerLine = "builder.Services.AddScoped<IReportService, ShadowForge.Services.Reporting.ReportService>();"
$usingLine    = "using ShadowForge.Services.Reporting;"

if ($programText -notlike "*IReportService*") {
    # Insert using at top (after the last existing using)
    $programText = $programText -replace "(using ShadowForge\.Web\.Services\.DashboardStateService\(\);)", "`$1`n$registerLine"
    # Actually insert after the last AddScoped block
    $insertAfter = "builder.Services.AddScoped<ShadowForge.Web.Services.DashboardStateService>();"
    $programText = $programText.Replace($insertAfter, "$insertAfter`n$registerLine")
    Set-Content -Path $programCs -Value $programText -NoNewline
    Write-Host "  [OK] Program.cs - IReportService registered" -ForegroundColor Green
} else {
    Write-Host "  [SKIP] Program.cs - IReportService already registered" -ForegroundColor Yellow
}

# -- 6. Add PuppeteerSharp NuGet to ShadowForge.Services.csproj ---------------
$servicesCsproj = "$RepoRoot\ShadowForge.Services\ShadowForge.Services.csproj"
$csprojText     = Get-Content $servicesCsproj -Raw

if ($csprojText -notlike "*PuppeteerSharp*") {
    $insertBefore = "</ItemGroup>"
    $puppeteerLine = '    <PackageReference Include="PuppeteerSharp" Version="20.0.3" />'
    # Insert before the closing </ItemGroup> of the last PackageReference block
    $csprojText = $csprojText -replace '(\s*<PackageReference Include="Bogus"[^/]*/>\s*</ItemGroup>)', "`$1"
    $csprojText = [regex]::Replace($csprojText, '(<PackageReference Include="Bogus"[^/]*/>)', "`$1`n$puppeteerLine")
    Set-Content -Path $servicesCsproj -Value $csprojText -NoNewline
    Write-Host "  [OK] ShadowForge.Services.csproj - PuppeteerSharp added" -ForegroundColor Green
} else {
    Write-Host "  [SKIP] ShadowForge.Services.csproj - PuppeteerSharp already present" -ForegroundColor Yellow
}

# -- 7. Add JS interop helper to App.razor (download trigger) -----------------
$appRazor    = "$RepoRoot\ShadowForge.Web\Components\App.razor"
$appRazorTxt = Get-Content $appRazor -Raw

$jsSnippet = @'
<script>
    window.shadowforgeDownloadPdf = function (base64, filename) {
        const bytes = atob(base64);
        const arr = new Uint8Array(bytes.length);
        for (let i = 0; i < bytes.length; i++) arr[i] = bytes.charCodeAt(i);
        const blob = new Blob([arr], { type: 'application/pdf' });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };
</script>
'@

if ($appRazorTxt -notlike "*shadowforgeDownloadPdf*") {
    # Append before </body> if present, otherwise just append
    if ($appRazorTxt -like "*</body>*") {
        $appRazorTxt = $appRazorTxt.Replace("</body>", "$jsSnippet`n</body>")
    } else {
        $appRazorTxt += "`n$jsSnippet"
    }
    Set-Content -Path $appRazor -Value $appRazorTxt -NoNewline
    Write-Host "  [OK] App.razor - JS download helper injected" -ForegroundColor Green
} else {
    Write-Host "  [SKIP] App.razor - JS helper already present" -ForegroundColor Yellow
}

# -- 8. dotnet restore + build -------------------------------------------------
Write-Host ""
Write-Host "  Running dotnet restore..." -ForegroundColor Cyan
Push-Location "$RepoRoot\ShadowForge.Web"
dotnet restore 2>&1 | Where-Object { $_ -match "error|warning|Restored" } | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

Write-Host "  Running dotnet build..." -ForegroundColor Cyan
$buildResult = dotnet build --no-restore 2>&1
$buildResult | Where-Object { $_ -match "error|warning|Build succeeded" } | ForEach-Object {
    if ($_ -match "error") { Write-Host "  $_" -ForegroundColor Red }
    elseif ($_ -match "warning") { Write-Host "  $_" -ForegroundColor Yellow }
    else { Write-Host "  $_" -ForegroundColor Green }
}
Pop-Location

Write-Host ""
Write-Host "  Done! Summary of changes:" -ForegroundColor Cyan
Write-Host "  -----------------------------------------------------" -ForegroundColor Gray
Write-Host "  Fix 1 -> ThreatIntel.razor   : severity filter UI added" -ForegroundColor White
Write-Host "  Fix 2 -> Reports.razor        : PDF export button added" -ForegroundColor White
Write-Host "  Fix 3 -> ReportService.cs     : full PDF generator (HTML->PuppeteerSharp)" -ForegroundColor White
Write-Host "  Fix 4 -> app.css              : filter button styles" -ForegroundColor White
Write-Host "  Fix 5 -> Program.cs           : IReportService DI registered" -ForegroundColor White
Write-Host "  Fix 6 -> Services.csproj      : PuppeteerSharp NuGet added" -ForegroundColor White
Write-Host "  Fix 7 -> App.razor            : JS PDF download interop" -ForegroundColor White
Write-Host ""
Write-Host "  FIRST RUN NOTE:" -ForegroundColor Yellow
Write-Host "  PuppeteerSharp will auto-download Chromium (~170 MB) on first PDF export." -ForegroundColor Yellow
Write-Host "  This is a one-time operation, cached in your temp folder." -ForegroundColor Yellow
Write-Host ""
