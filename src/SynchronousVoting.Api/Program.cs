using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SynchronousVoting.Api.Hubs;
using Voting.Api.Common;
using Voting.Api.Common.RequestTiming;
using Voting.Application;
using Voting.Infrastructure;
using Voting.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddVotingCors();
builder.Services.AddVoteRateLimiter();
builder.Services.AddApplicationServices();
builder.Services.AddProjectionDelayOptions(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();

builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddGlobalExceptionHandling();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Synchronous Voting API", Version = "v1" });
});

const string serviceName = "SynchronousVoting.Api";
builder.Services.AddVotingOpenTelemetry(serviceName, "SynchronousVoting.Api.Metrics");

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "database",
        tags: new[] { "ready" });

var app = builder.Build();

app.ApplyMigrations();

app.UseRequestTiming();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.UseGlobalExceptionHandling();
app.UseRateLimiter();
app.UseAuthorization();
app.UseCors("AllowFrontend");
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapControllers();
app.MapHub<ResultsHub>("/hubs/results");

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<VotingDbContext>();
        dbContext.Database.EnsureCreated();
    }
}

app.Run();