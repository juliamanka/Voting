using HybridVoting.Api;
using HybridVoting.Api.Hubs;
using HybridVoting.Api.Messaging.Consumers;
using MassTransit;
using Voting.Application;
using Voting.Application.Interfaces;
using Voting.Application.Services;
using Voting.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------
// KONFIGURACJA SERWISÓW
// ---------------------------

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

// SignalR
builder.Services.AddSignalR();

// CORS – dla Angulara (domyślnie http://localhost:4200, ale możesz dać z appsettings: FrontendUrl)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// IPollService – jeśli masz implementację serwisu do wyników (używaną przez VoteRecordedEventConsumer)
// Jeżeli już rejestrujesz PollService w innym miejscu/projekcie, tę linię możesz usunąć lub dostosować.
builder.Services.AddScoped<IPollService, PollService>();

// HYBRYDOWA implementacja IVoteNotifier
builder.Services.AddScoped<IVoteNotifier, HybridVoteNotifier>();

// MassTransit + RabbitMQ – tylko VoteRecordedEventConsumer
builder.Services.AddMassTransit(cfg =>
{
    cfg.AddConsumer<VoteRecordedEventConsumer>();

    cfg.UsingRabbitMq((context, busCfg) =>
    {
        var host = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var username = builder.Configuration["RabbitMq:Username"] ?? "guest";
        var password = builder.Configuration["RabbitMq:Password"] ?? "guest";

        busCfg.Host(host, "/", h =>
        {
            h.Username(username);
            h.Password(password);
        });

        // Endpoint do obsługi VoteRecordedEvent
        busCfg.ReceiveEndpoint("vote-recorded-events", e =>
        {
            e.ConfigureConsumer<VoteRecordedEventConsumer>(context);
        });
    });
});

// Opcjonalnie, jeśli chcesz używać MassTransit HostedService
// builder.Services.AddMassTransitHostedService();

var app = builder.Build();

// ---------------------------
// MIDDLEWARE PIPELINE
// ---------------------------


    app.UseSwagger();
    app.UseSwaggerUI();


app.UseHttpsRedirection();

// CORS przed routingiem
app.UseCors("AllowFrontend");

// Jeśli dodasz autoryzację, to tu:
// app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SignalR hub dla wyników głosowania
app.MapHub<ResultsHub>("/hubs/results");

app.Run();