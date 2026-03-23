using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bogus;
using Microsoft.Extensions.Logging;
using ShadowForge.Core.Interfaces;
using ShadowForge.Core.Models;

namespace ShadowForge.Services.UserGen;

/// <summary>
/// Generates realistic fake enterprise users using randomuser.me API.
/// Falls back to Bogus library for offline/demo scenarios.
/// Demonstrates: Strategy pattern (API vs local), async enumeration, factory methods.
/// </summary>
public sealed class UserGenService : IUserGenService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserGenService> _logger;
    private readonly Faker _faker = new("en");

    private static readonly string[] Departments =
    [
        "Engineering", "Finance", "HR", "Marketing", "Operations",
        "Legal", "IT Security", "Sales", "Research & Development", "Executive"
    ];

    private static readonly Dictionary<string, string[]> JobTitlesByDept = new()
    {
        ["Engineering"]          = ["Software Engineer", "DevOps Engineer", "Tech Lead", "Principal Engineer"],
        ["Finance"]              = ["Financial Analyst", "Controller", "CFO", "Accounts Manager"],
        ["HR"]                   = ["HR Specialist", "Talent Acquisition", "CHRO", "People Ops"],
        ["Marketing"]            = ["Marketing Manager", "Content Strategist", "CMO", "SEO Analyst"],
        ["Operations"]           = ["Operations Manager", "Project Manager", "COO", "Process Analyst"],
        ["Legal"]                = ["Legal Counsel", "Compliance Officer", "General Counsel", "Paralegal"],
        ["IT Security"]          = ["Security Analyst", "CISO", "SOC Engineer", "Pen Tester"],
        ["Sales"]                = ["Account Executive", "Sales Manager", "VP Sales", "SDR"],
        ["Research & Development"] = ["Research Scientist", "R&D Lead", "CTO", "Data Scientist"],
        ["Executive"]            = ["CEO", "COO", "VP Operations", "Chief of Staff"]
    };

    public UserGenService(IHttpClientFactory httpClientFactory, ILogger<UserGenService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("RandomUser");
        _logger     = logger;
    }

    public async Task<IEnumerable<FakeUser>> GenerateUsersAsync(int count = 50)
    {
        try
        {
            _logger.LogInformation("Fetching {Count} users from randomuser.me...", count);
            var response = await _httpClient.GetFromJsonAsync<RandomUserResponse>(
                $"api/?results={count}&nat=us,gb,ca&inc=name,email,login,phone,picture,location");

            if (response?.Results is { Count: > 0 })
            {
                _logger.LogInformation("Successfully fetched {Count} users from API.", response.Results.Count);
                return response.Results.Select(EnrichWithEnterprise);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "randomuser.me API unavailable. Using Bogus fallback.");
        }

        return GenerateBogusUsers(count);
    }

    public async Task<FakeUser> GenerateSingleUserAsync()
    {
        var users = await GenerateUsersAsync(1);
        return users.First();
    }

    public FakeUser GenerateFromTemplate(UserTemplate template)
    {
        var user = GenerateBogusUser();
        user.Department  = template.Department;
        user.JobTitle    = template.JobTitle;
        user.IsAdminAccount = template.IsAdmin;
        return user;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private FakeUser EnrichWithEnterprise(RandomUserResult r)
    {
        var dept  = Departments[Random.Shared.Next(Departments.Length)];
        var titles = JobTitlesByDept.GetValueOrDefault(dept, ["Employee"]);
        var title  = titles[Random.Shared.Next(titles.Length)];

        return new FakeUser
        {
            FirstName    = r.Name.First,
            LastName     = r.Name.Last,
            Email        = r.Email,
            Username     = r.Login?.Username ?? r.Email.Split('@')[0],
            PhoneNumber  = r.Phone ?? string.Empty,
            AvatarUrl    = r.Picture?.Thumbnail ?? string.Empty,
            Department   = dept,
            JobTitle     = title,
            IpAddress    = GenerateFakeInternalIp(),
            IsAdminAccount = title.Contains("CIO") || title.Contains("CISO") || title.Contains("CEO")
        };
    }

    private IEnumerable<FakeUser> GenerateBogusUsers(int count)
        => Enumerable.Range(0, count).Select(_ => GenerateBogusUser());

    private FakeUser GenerateBogusUser()
    {
        var dept  = _faker.PickRandom(Departments);
        var titles = JobTitlesByDept.GetValueOrDefault(dept, ["Employee"]);
        var title  = _faker.PickRandom(titles);

        return new FakeUser
        {
            FirstName    = _faker.Name.FirstName(),
            LastName     = _faker.Name.LastName(),
            Email        = _faker.Internet.Email(),
            Username     = _faker.Internet.UserName(),
            PhoneNumber  = _faker.Phone.PhoneNumber(),
            AvatarUrl    = $"https://i.pravatar.cc/150?u={Guid.NewGuid()}",
            Department   = dept,
            JobTitle     = title,
            IpAddress    = GenerateFakeInternalIp(),
            IsAdminAccount = title.Contains("CIO") || title.Contains("CISO") || title.Contains("CEO")
        };
    }

    private static string GenerateFakeInternalIp()
        => $"10.0.{Random.Shared.Next(1, 10)}.{Random.Shared.Next(2, 254)}";

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private record RandomUserResponse([property: JsonPropertyName("results")] List<RandomUserResult>? Results);
    private record RandomUserResult(
        [property: JsonPropertyName("name")]    RandomUserName Name,
        [property: JsonPropertyName("email")]   string Email,
        [property: JsonPropertyName("login")]   RandomUserLogin? Login,
        [property: JsonPropertyName("phone")]   string? Phone,
        [property: JsonPropertyName("picture")] RandomUserPicture? Picture
    );
    private record RandomUserName(
        [property: JsonPropertyName("first")] string First,
        [property: JsonPropertyName("last")]  string Last
    );
    private record RandomUserLogin([property: JsonPropertyName("username")] string Username);
    private record RandomUserPicture([property: JsonPropertyName("thumbnail")] string Thumbnail);
}

