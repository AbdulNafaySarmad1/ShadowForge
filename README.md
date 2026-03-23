# ShadowForge 🔥
### Enterprise-Grade Adversary Emulation Platform · C# / .NET 8 / Blazor Server

> A modular cybersecurity simulation dashboard built as a C# OOP showcase project.
> Pulls **live threat intelligence** from AlienVault OTX and AbuseIPDB.
> Generates **realistic fake enterprise users** from randomuser.me.
> Maps all simulated activity to the **MITRE ATT&CK framework**.

---

## Architecture Overview

```
ShadowForge/
├── ShadowForge.Core/          # Interfaces, domain models, abstract base classes
│   ├── Interfaces/            # IModule, IRepository<T>, IThreatIntelService, ...
│   ├── Models/                # DomainModels.cs — all value objects and entities
│   └── Abstractions/          # BaseModule (Template Method pattern)
│
├── ShadowForge.Services/      # Business logic, simulation modules, API clients
│   ├── ThreatIntel/           # ThreatIntelService — OTX + AbuseIPDB live calls
│   ├── UserGen/               # UserGenService — randomuser.me + Bogus fallback
│   ├── Network/               # NetworkSimService — realistic host/port simulation
│   ├── SimulationModules.cs   # ReconModule, LateralMovementModule, PersistenceModule, ReportModule
│   └── ModuleRegistry.cs      # Factory + Registry pattern, SimulationOrchestrator
│
├── ShadowForge.Data/          # EF Core — SQLite, repositories, entities
│   ├── ShadowForgeDbContext.cs
│   └── Repositories/          # Generic Repository<T>, SessionRepository, EventRepository
│
└── ShadowForge.Web/           # Blazor Server UI + SignalR hub
    ├── Components/Pages/      # Dashboard, Simulation, ThreatIntel, NetworkMap, MITRE, Users, Reports
    ├── Hubs/                  # SimulationHub.cs — real-time event broadcast
    ├── Services/              # DashboardStateService — reactive state container
    └── wwwroot/               # app.css — dark hacker aesthetic
```

---

## OOP Concepts Demonstrated

| Concept | Where |
|---|---|
| **Interfaces** | `IModule`, `IRepository<T>`, `IThreatIntelService`, `IUserGenService` |
| **Abstract classes** | `BaseModule` — Template Method pattern |
| **Inheritance** | `PersistentBaseModule : BaseModule`, all 4 concrete modules |
| **Polymorphism** | `ExecuteAsync()` dispatches to each module's `ExecuteCoreAsync()` override |
| **Encapsulation** | Private service state, readonly collections, internal mutation methods |
| **Generics** | `IRepository<T>`, `Repository<T>`, `ApiResult<T>` |
| **Interfaces (multiple)** | `IRealtimeModule`, `IPersistentModule` on same class |
| **Design Patterns** | Factory, Registry, Observer, Template Method, Strategy, Repository |
| **Async / TPL** | `Task.WhenAll`, `CancellationToken`, `async/await` throughout |
| **LINQ** | Complex aggregation in `ReportModule`, `EventRepository` analytics |
| **DI / IoC** | Full ASP.NET Core DI — `IServiceProvider`, `AddScoped/Transient` |
| **EF Core** | Code-first, Fluent API, `DbContext`, migrations, repository pattern |

---

## Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- (Optional) Free API keys — app runs with demo data without them

### 1. Get free API keys
| Service | URL | What it gives you |
|---|---|---|
| AlienVault OTX | https://otx.alienvault.com | Live IOC feeds, IP/domain intel |
| AbuseIPDB | https://www.abuseipdb.com/api | IP reputation scores |

### 2. Add your keys
Edit `ShadowForge.Web/appsettings.json`:
```json
{
  "ApiKeys": {
    "OTX":       "your_actual_otx_key",
    "AbuseIPDB": "your_actual_abuseipdb_key"
  }
}
```
> **No keys?** The app ships with fallback demo data — everything still works.

### 3. Run
```bash
cd ShadowForge
dotnet run --project ShadowForge.Web
```
Open `https://localhost:5001` in your browser.

### 4. First run
1. Click **Run Simulation** in the sidebar
2. Leave the default subnet and all modules selected
3. Hit **Launch Simulation**
4. Watch the live event feed on the Dashboard tab in real time
5. After completion — check MITRE ATT&CK, Network Map, and Reports

---

## Module Reference

### Reconnaissance
Simulates network scanning on a given subnet. Discovers 8–20 realistic hosts with OS fingerprinting, open ports, and running services. Enriches discovered IPs via AbuseIPDB + OTX. Fetches live IOC pulses.
- MITRE: T1046 (Network Service Discovery), T1590 (Gather Victim Network Information)

### Lateral Movement
Simulates adversary pivoting between discovered hosts using realistic enterprise techniques — RDP, SMB, WMI, WinRM, SSH.
- MITRE: T1021.001, T1021.002, T1021.006, T1047, T1563.001

### Persistence
Demonstrates conceptual persistence techniques: scheduled tasks, registry run keys, Windows services, local account creation. All simulated — no actual system changes.
- MITRE: T1053.005, T1547.001, T1543.003, T1136.001

### Report Generator
Aggregates all module results, groups events by MITRE technique ID using LINQ, and produces a structured simulation report.

---

## Notes
- **Ethical guardrails**: No actual network scanning — all hosts are generated in-memory
- **Lab only**: IP whitelist enforcement can be added in `NetworkSimService`
- **Extensibility**: Add new modules by implementing `IModule` and registering in `ModuleRegistry`
