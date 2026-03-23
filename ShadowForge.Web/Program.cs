using Microsoft.EntityFrameworkCore;
using ShadowForge.Core.Interfaces;
using ShadowForge.Data;
using ShadowForge.Data.Repositories;
using ShadowForge.Services;
using ShadowForge.Services.Network;
using ShadowForge.Services.ThreatIntel;
using ShadowForge.Services.UserGen;
using ShadowForge.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSignalR();

builder.Services.AddDbContext<ShadowForgeDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=shadowforge.db"));

builder.Services.AddScoped<SessionRepository>();
builder.Services.AddScoped<EventRepository>();

builder.Services.AddHttpClient("OTX", client =>
{
    client.BaseAddress = new Uri("https://otx.alienvault.com/");
    client.DefaultRequestHeaders.Add("X-OTX-API-KEY",
        builder.Configuration["ApiKeys:OTX"] ?? "YOUR_OTX_KEY_HERE");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient("AbuseIPDB", client =>
{
    client.BaseAddress = new Uri("https://api.abuseipdb.com/");
    client.DefaultRequestHeaders.Add("Key",
        builder.Configuration["ApiKeys:AbuseIPDB"] ?? "YOUR_ABUSEIPDB_KEY_HERE");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("RandomUser", client =>
{
    client.BaseAddress = new Uri("https://randomuser.me/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<IThreatIntelService, ThreatIntelService>();
builder.Services.AddScoped<IUserGenService, UserGenService>();
builder.Services.AddScoped<INetworkSimService, NetworkSimService>();

builder.Services.AddTransient<ReconModule>();
builder.Services.AddTransient<LateralMovementModule>();
builder.Services.AddTransient<PersistenceModule>();
builder.Services.AddTransient<ReportModule>();

builder.Services.AddScoped<ModuleRegistry>();
builder.Services.AddScoped<SimulationOrchestrator>();
builder.Services.AddScoped<ShadowForge.Web.Services.DashboardStateService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShadowForgeDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ShadowForge.Web.Components.App>().AddInteractiveServerRenderMode();
app.MapHub<SimulationHub>("/hubs/simulation");

app.Run();
