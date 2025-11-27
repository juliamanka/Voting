using System.Reflection;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Voting.Api.Common;
using Voting.Application;
using Voting.Infrastructure;
using Voting.Infrastructure.Database;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", Assembly.GetExecutingAssembly().GetName().Name)
    .Enrich.WithEnvironmentName()
    .WriteTo.Console() // Logowanie do konsoli
    .WriteTo.OpenTelemetry(opts => // Wysyłanie logów do kolektora OTLP (np. Loki)
    {
        opts.Endpoint = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build()
            .GetValue<string>("OtlpExporter:Endpoint");
    })
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Host.UseSerilog();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithOrigins("http://localhost:4200")
                .AllowCredentials();
        });
    });
    
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    builder.Services.AddControllers();
    
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddProblemDetails(); 

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Synchronous Voting API", Version = "v1" });
    });
    
    
    const string serviceName = "SynchronousVoting.Api";
    var otlpEndpoint = builder.Configuration["OtlpExporter:Endpoint"];

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(serviceName))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation() 
            .AddEntityFrameworkCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(serviceName)
            .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint!)) 
            .AddConsoleExporter()) 
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation() 
            .AddRuntimeInstrumentation() 
            .AddProcessInstrumentation() 
            .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint!))
            .AddConsoleExporter()); 

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" }) // Podstawowy check
        .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!, // Check bazy danych
                      name: "database", 
                      tags: new[] { "ready" });
    
    var app = builder.Build();

    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseSerilogRequestLogging();
    
    app.MapMetrics(); 
    
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

    app.UseGlobalExceptionHandling();
    app.UseAuthorization();
    app.UseCors("AllowFrontend");
    app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VotingDbContext>();
            dbContext.Database.EnsureCreated(); 
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application could not be run.");
}
finally
{
    Log.CloseAndFlush();
}