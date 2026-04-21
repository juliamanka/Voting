using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Voting.Api.Common.RequestTiming;

public static class RequestTimingContext
{
    private const string RequestStartedAtUtcItemKey = "__request_started_at_utc";
    private const string RequestStartTimestampItemKey = "__request_start_timestamp";

    public static void SetRequestStart(HttpContext context, DateTime requestStartedAtUtc)
    {
        context.Items[RequestStartedAtUtcItemKey] = requestStartedAtUtc;
        context.Items[RequestStartTimestampItemKey] = Stopwatch.GetTimestamp();
    }

    public static DateTime GetRequestStartedAtUtc(HttpContext? context, DateTime fallbackUtc)
    {
        if (context?.Items.TryGetValue(RequestStartedAtUtcItemKey, out var value) == true &&
            value is DateTime requestStartedAtUtc)
        {
            return requestStartedAtUtc;
        }

        return fallbackUtc;
    }

    public static TimeSpan GetElapsedSinceRequestStart(HttpContext? context)
    {
        if (context?.Items.TryGetValue(RequestStartTimestampItemKey, out var value) == true &&
            value is long startTimestamp)
        {
            return Stopwatch.GetElapsedTime(startTimestamp);
        }

        return TimeSpan.Zero;
    }
}

public static class RequestTimingApplicationBuilderExtensions
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
