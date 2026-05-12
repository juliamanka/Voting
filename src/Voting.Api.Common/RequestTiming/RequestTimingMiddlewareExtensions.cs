using Microsoft.AspNetCore.Builder;

namespace Voting.Api.Common.RequestTiming;

public static class RequestTimingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            RequestTimingContext.SetRequestStart(context, DateTime.UtcNow);
            await next();
        });
    }
}