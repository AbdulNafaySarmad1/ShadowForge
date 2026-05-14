$file = "D:\XboxGames\UNI projects\shadowforge\ShadowForge.Services\ModuleRegistry.cs"
$content = [System.IO.File]::ReadAllText($file)
$old = "            // Inject all previous events into context for ReportModule`r`n            var allPrevEvents = results.SelectMany(r => r.Events).ToList();`r`n            var enrichedParams = new Dictionary<string, string>(context.Parameters)`r`n            {`r`n                [""__all_events_json""] = System.Text.Json.JsonSerializer.Serialize(allPrevEvents)`r`n            };`r`n            var enrichedContext = new ModuleContext(context.SessionId, context.TargetSubnet, enrichedParams);`r`n            var result = await module.ExecuteAsync(enrichedContext, ct);"
$new = "            SimulationEventStore.Set(context.SessionId, results.SelectMany(r => r.Events).ToList());`r`n            var result = await module.ExecuteAsync(context, ct);"
$out = $content.Replace($old, $new)
if ($out -eq $content) { Write-Host "NO MATCH registry"; exit 1 }
[System.IO.File]::WriteAllText($file, $out, [System.Text.Encoding]::UTF8)
Write-Host "Done registry"
