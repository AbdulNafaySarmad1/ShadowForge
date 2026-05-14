$file = "D:\XboxGames\UNI projects\shadowforge\ShadowForge.Services\SimulationModules.cs"
$content = [System.IO.File]::ReadAllText($file)
$old = "        var allEvents = new List<SimulationEvent>(result.Events);`r`n        if (context.Parameters.TryGetValue(""__all_events_json"", out var eventsJson))`r`n        {`r`n            var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };`r`n            var prev = System.Text.Json.JsonSerializer.Deserialize<List<SimulationEvent>>(eventsJson, jsonOpts);`r`n            if (prev is not null) allEvents.AddRange(prev);`r`n        }"
$old2 = "        var allEvents = new List<SimulationEvent>(result.Events);`r`n        if (context.Parameters.TryGetValue(""__all_events_json"", out var eventsJson))`r`n        {`r`n            var prev = System.Text.Json.JsonSerializer.Deserialize<List<SimulationEvent>>(eventsJson);`r`n            if (prev is not null) allEvents.AddRange(prev);`r`n        }"
$new = "        var allEvents = SimulationEventStore.Get(context.SessionId);"
$out = $content.Replace($old, $new)
if ($out -eq $content) { $out = $content.Replace($old2, $new) }
if ($out -eq $content) { Write-Host "NO MATCH modules"; exit 1 }
[System.IO.File]::WriteAllText($file, $out, [System.Text.Encoding]::UTF8)
Write-Host "Done modules"
