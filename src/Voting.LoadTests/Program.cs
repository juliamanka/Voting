using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

// --------------------------------------------------
//  MODELE – dopasowane do Twojego API
// --------------------------------------------------

public class PollOptionDto
{
    [JsonPropertyName("pollOptionId")]
    public Guid PollOptionId { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class PollDto
{
    [JsonPropertyName("pollId")]
    public Guid PollId { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("options")]
    public List<PollOptionDto> Options { get; set; } = new();
}

public class VoteRequestDto
{
    [JsonPropertyName("pollId")]
    public Guid PollId { get; set; }

    [JsonPropertyName("pollOptionId")]
    public Guid PollOptionId { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }
}

// --------------------------------------------------
//  Program z Main
// --------------------------------------------------

public class Program
{
    private const string StepName = "vote_step";
    private static readonly ConcurrentDictionary<string, PollCacheEntry> PollCache = new();

    private sealed record PollCacheEntry(Guid PollId, Guid[] OptionIds);

    public static void Main(string[] args)
    {
        // 1. Czy mamy listę wielu URL-i? (dla skalowania horyzontalnego)
        //    Priorytet:
        //    1) env: VOTING_API_BASE_URLS = "http://localhost:5101,http://localhost:5102,..."
        //    2) env: VOTING_API_BASE_URL = "http://localhost:5001"
        //    3) args[0]
        //    4) domyślnie: "http://localhost:5001"
        var baseUrlsEnv = Environment.GetEnvironmentVariable("VOTING_API_BASE_URLS");

        string[] baseUrls;

        if (!string.IsNullOrWhiteSpace(baseUrlsEnv))
        {
            baseUrls = baseUrlsEnv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            Console.WriteLine("[NBomber] Using multiple base URLs from VOTING_API_BASE_URLS:");
            foreach (var u in baseUrls)
            {
                Console.WriteLine($"  - {u}");
            }
        }
        else
        {
            var singleBaseUrl =
                Environment.GetEnvironmentVariable("VOTING_API_BASE_URL")
                ?? (args.Length > 0 ? args[0] : "http://localhost:5001");

            baseUrls = new[] { singleBaseUrl };

            Console.WriteLine("[NBomber] Using single base URL:");
            Console.WriteLine($"  - {singleBaseUrl}");
        }

        var architecture = Environment.GetEnvironmentVariable("ARCHITECTURE")
                           ?? (args.Length > 1 ? args[1] : "sync");
        var loadProfile = Environment.GetEnvironmentVariable("LOAD_PROFILE")
                          ?? (args.Length > 2 ? args[2] : "staircase");

        Console.WriteLine($"[NBomber] architecture = {architecture}");
        Console.WriteLine($"[NBomber] load profile = {loadProfile}");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Jeden HttpClient na cały test (bez BaseAddress, bo używamy pełnych URL-i).
        // UseCookies=false omija problem inicjalizacji CookieContainer na części środowisk macOS.
        var httpClient = new HttpClient(new HttpClientHandler { UseCookies = false });

        // 2. Scenariusz NBomber 6.x
        var scenario = Scenario.Create(
                name: $"{architecture}_vote_scenario",
                async context =>
                {
                    try
                    {
                        // wybór instancji API – prosty random (równomierne rozłożenie ruchu)
                        var apiBase = baseUrls.Length == 1
                            ? baseUrls[0]
                            : baseUrls[Random.Shared.Next(baseUrls.Length)];

                        // Głosujemy na już-pobranej ankiecie, żeby mierzyć sam path /api/vote
                        var poll = await GetOrLoadPollAsync(httpClient, apiBase, jsonOptions, context.ScenarioCancellationToken);
                        var optionId = poll.OptionIds[Random.Shared.Next(poll.OptionIds.Length)];

                        var vote = new VoteRequestDto
                        {
                            PollId = poll.PollId,
                            PollOptionId = optionId,
                            UserId = Guid.NewGuid().ToString()
                        };

                        var voteResponse = await httpClient.PostAsJsonAsync(
                            $"{apiBase}/api/vote",
                            vote,
                            jsonOptions,
                            context.ScenarioCancellationToken);

                        if (!voteResponse.IsSuccessStatusCode)
                        {
                            return Response.Fail(
                                StepName,
                                $"POST /api/vote failed with {voteResponse.StatusCode}",
                                null,
                                0,
                                0L
                            );
                        }

                        return Response.Ok<object>(StepName);
                    }
                    catch (Exception ex)
                    {
                        return Response.Fail(
                            StepName,
                            ex.Message,
                            null,
                            0,
                            0L
                        );
                    }
                })
            .WithWarmUpDuration(TimeSpan.FromSeconds(10))
               .WithLoadSimulations(BuildLoadSimulations(loadProfile));

        // 3. Uruchomienie
        NBomberRunner
            .RegisterScenarios(scenario)
            .WithTestSuite("Voting")
            .WithTestName($"NBomber_{architecture}_RPS")
            .WithReportFileName($"NBomber_{architecture}_RPS")
            .WithReportFolder("nbomber-reports")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
            .Run();
    }

    private static async Task<PollCacheEntry> GetOrLoadPollAsync(
        HttpClient httpClient,
        string apiBase,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        if (PollCache.TryGetValue(apiBase, out var cached))
            return cached;

        var pollsResponse = await httpClient.GetAsync($"{apiBase}/api/polls", cancellationToken);
        if (!pollsResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"GET /api/polls failed with {pollsResponse.StatusCode}");

        var pollsJson = await pollsResponse.Content.ReadAsStringAsync(cancellationToken);
        var polls = JsonSerializer.Deserialize<List<PollDto>>(pollsJson, jsonOptions);

        if (polls is null || polls.Count == 0)
            throw new InvalidOperationException("No polls returned from /api/polls");

        var firstPoll = polls[0];
        if (firstPoll.Options is null || firstPoll.Options.Count == 0)
            throw new InvalidOperationException("Poll has no options");

        var loaded = new PollCacheEntry(
            firstPoll.PollId,
            firstPoll.Options.Select(x => x.PollOptionId).ToArray()
        );

        PollCache.TryAdd(apiBase, loaded);
        return loaded;
    }

    private static LoadSimulation[] BuildLoadSimulations(string loadProfile)
    {
        if (string.Equals(loadProfile, "steady", StringComparison.OrdinalIgnoreCase))
        {
            var steadyRps = ReadInt("STEADY_RPS", 5);
            var steadyMinutes = ReadInt("STEADY_MINUTES", 6);

            Console.WriteLine($"[NBomber] steady profile: {steadyRps} RPS for {steadyMinutes} min");

            return new[]
            {
                Simulation.Inject(
                    rate: steadyRps,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromMinutes(steadyMinutes))
            };
        }

        var rawRates = Environment.GetEnvironmentVariable("STAIR_RATES") ?? "5,10,50,100";
        var stepMinutes = ReadInt("STAIR_STEP_MINUTES", 2);
        var rates = rawRates
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();

        Console.WriteLine(
            $"[NBomber] staircase profile: rates=[{string.Join(", ", rates)}], step={stepMinutes} min");

        return rates.Select(rate =>
                Simulation.Inject(
                    rate: rate,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromMinutes(stepMinutes)))
            .ToArray();
    }

    private static int ReadInt(string envName, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
    }
}
