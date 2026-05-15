using AsynchronousVoting.Api.Hubs;
using AsynchronousVoting.Api.Messaging.Consumers;
using AsynchronousVoting.Api.Notifiers;
using MassTransit;
using Voting.Api.Common;
using Voting.Api.Common.RequestTiming;
using Voting.Application;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Application.Messaging;
using Voting.Infrastructure;
using Voting.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "AllowFrontend";

builder.Services.AddVotingCors(CorsPolicy);

builder.Services.AddVoteRateLimiter();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplicationServices();
builder.Services.AddProjectionDelayOptions(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddSignalR();

builder.Services.AddGlobalExceptionHandling();

const string serviceName = "AsynchronousVoting.Api";
builder.Services.AddVotingOpenTelemetry(serviceName, "AsynchronousVoting.Api.Metrics");
builder.Services.AddVotingSqlHealthChecks(builder.Configuration);

var rabbitSection = builder.Configuration.GetSection("RabbitMq");
if (!rabbitSection.Exists())
    throw new InvalidOperationException("No section 'RabbitMq' in appsettings.json.");

var rabbitHost = rabbitSection["Host"] ?? throw new InvalidOperationException("RabbitMq:Host is missing");
var rabbitUser = rabbitSection["Username"] ?? throw new InvalidOperationException("RabbitMq:Username is missing");
var rabbitPass = rabbitSection["Password"] ?? throw new InvalidOperationException("RabbitMq:Password is missing");

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PollResultsUpdatedEventConsumer>();

    x.AddEntityFrameworkOutbox<VotingDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
        o.DisableInboxCleanupService(); 
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });
        
        cfg.Message<PollResultsUpdatedEvent>(m => m.SetEntityName("async-poll-results-updated-exchange"));
        
        cfg.ReceiveEndpoint(VoteQueueNames.AsyncPollResultsUpdatedEventsQueue,
            e =>
            {
                e.UseEntityFrameworkOutbox<VotingDbContext>(context);
                e.ConfigureConsumer<PollResultsUpdatedEventConsumer>(context);
                e.ConcurrentMessageLimit = 4;
                e.PrefetchCount = 8;
            });
    });
});

builder.Services.AddScoped<IVoteSubmissionPublisher, VoteSubmissionPublisher>();

var app = builder.Build();

app.ApplyMigrations();

app.UseRequestTiming();
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);

app.UseGlobalExceptionHandling();

app.UseRateLimiter();

app.UseAuthorization();

app.MapVotingJsonHealthChecks();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapControllers();
app.MapHub<ResultsHub>("/hubs/results");

app.Run();
