$file = "D:\XboxGames\UNI projects\shadowforge\ShadowForge.Web\Components\Pages\Simulation.razor"
$content = [System.IO.File]::ReadAllText($file)
$old = "                var result = await Orchestrator.RunModuleAsync(moduleName, context, _cts.Token);`r`n                _results.Add(result);"
$new = "                var result = await Orchestrator.RunModuleAsync(moduleName, context, _cts.Token);`r`n                _results.Add(result);`r`n                SimulationEventStore.Set(context.SessionId, _results.SelectMany(r => r.Events).ToList());"
$out = $content.Replace($old, $new)
if ($out -eq $content) { Write-Host "NO MATCH"; exit 1 }
[System.IO.File]::WriteAllText($file, $out, [System.Text.Encoding]::UTF8)
Write-Host "Done"
