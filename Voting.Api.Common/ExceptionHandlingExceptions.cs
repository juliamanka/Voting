using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Voting.Api.Common.Middleware;

namespace Voting.Api.Common;

public static class ExceptionHandlingExtensions
{
    public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(); 
        return services;
    }

    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(); 
        return app;
    }
}