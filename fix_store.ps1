$file = "D:\XboxGames\UNI projects\shadowforge\ShadowForge.Services\SimulationModules.cs"
$content = [System.IO.File]::ReadAllText($file)
$storeClass = "`r`n`r`npublic static class SimulationEventStore`r`n{`r`n    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, List<SimulationEvent>> _store = new();`r`n    public static void Set(Guid sessionId, List<SimulationEvent> events) => _store[sessionId] = events;`r`n    public static List<SimulationEvent> Get(Guid sessionId) => _store.TryGetValue(sessionId, out var e) ? e : new();`r`n}"
[System.IO.File]::WriteAllText($file, $content + $storeClass, [System.Text.Encoding]::UTF8)
Write-Host "Done store"
