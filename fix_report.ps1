$file = "D:\XboxGames\UNI projects\shadowforge\ShadowForge.Services\SimulationModules.cs"
$content = [System.IO.File]::ReadAllText($file)
$old = "        await SimulateDelayAsync(200, 500, ct);`r`n`r`n        // Build MITRE ATT&CK mappings from all events via LINQ`r`n        var allMitreTechniques = result.Events"
$new = "        await SimulateDelayAsync(200, 500, ct);`r`n`r`n        var allEvents = new List<SimulationEvent>(result.Events);`r`n        if (context.Parameters.TryGetValue(`"__all_events_json`", out var eventsJson))`r`n        {`r`n            var prev = System.Text.Json.JsonSerializer.Deserialize<List<SimulationEvent>>(eventsJson);`r`n            if (prev is not null) allEvents.AddRange(prev);`r`n        }`r`n`r`n        // Build MITRE ATT&CK mappings from all events via LINQ`r`n        var allMitreTechniques = allEvents"
$result = $content.Replace($old, $new)
if ($result -eq $content) { Write-Host "NO MATCH - check line endings"; exit 1 }
[System.IO.File]::WriteAllText($file, $result, [System.Text.Encoding]::UTF8)
Write-Host "Done"
