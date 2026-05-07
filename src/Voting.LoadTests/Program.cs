using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;


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

public class Program
{
    private const string StepName = "vote_step";
    private static readonly ConcurrentDictionary<string, PollCacheEntry> PollCache = new();

    private sealed record PollCacheEntry(Guid PollId, Guid[] OptionIds);

    public static void Main(string[] args){
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

        var reportFolder = Environment.GetEnvironmentVariable("NBOMBER_REPORTS_DIR")
                           ?? Path.GetFullPath("nbomber-reports");
        Console.WriteLine($"[NBomber] report folder = {reportFolder}");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var httpClient = new HttpClient(new HttpClientHandler { UseCookies = false });

        var scenario = Scenario.Create(
                name: $"{architecture}_vote_scenario",
                async context =>
                {
                    try
                    {
                        var apiBase = baseUrls.Length == 1
                            ? baseUrls[0]
                            : baseUrls[Random.Shared.Next(baseUrls.Length)];

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

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithTestSuite("Voting")
            .WithTestName($"NBomber_{architecture}_RPS")
            .WithReportFileName($"NBomber_{architecture}_RPS")
            .WithReportFolder(reportFolder)
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

        if (string.Equals(loadProfile, "staircase", StringComparison.OrdinalIgnoreCase))
        {
            return BuildStepProfile(
                profileName: "staircase",
                ratesEnvName: "STAIR_RATES",
                ratesFallback: "5,10,50,100",
                stepMinutesEnvName: "STAIR_STEP_MINUTES",
                stepMinutesFallback: 2);
        }

        if (string.Equals(loadProfile, "burst", StringComparison.OrdinalIgnoreCase))
        {
            return BuildStepProfile(
                profileName: "burst",
                ratesEnvName: "BURST_RATES",
                ratesFallback: "10,150,10",
                stepMinutesEnvName: "BURST_STEP_MINUTES",
                stepMinutesFallback: 1);
        }

        throw new InvalidOperationException(
            $"Unsupported LOAD_PROFILE '{loadProfile}'. Expected one of: steady, staircase, burst.");
    }

    private static LoadSimulation[] BuildStepProfile(
        string profileName,
        string ratesEnvName,
        string ratesFallback,
        string stepMinutesEnvName,
        int stepMinutesFallback)
    {
        var rawRates = Environment.GetEnvironmentVariable(ratesEnvName) ?? ratesFallback;
        var stepMinutes = ReadInt(stepMinutesEnvName, stepMinutesFallback);
        var rates = rawRates
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();

        Console.WriteLine(
            $"[NBomber] {profileName} profile: rates=[{string.Join(", ", rates)}], step={stepMinutes} min");

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
