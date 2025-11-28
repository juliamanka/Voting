using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Voting.Application.Exceptions;
using ValidationException = FluentValidation.ValidationException;

namespace Voting.Api.Common.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception, "An unknown error occurred: {Message}", exception.Message);

        var problemDetails = CreateProblemDetails(httpContext, exception);

        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var status = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";
        var detail = exception.Message;

        switch (exception)
        {
            case ValidationException validationException:
                status = StatusCodes.Status400BadRequest;
                title = "Validation exception";
                var errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(k => k.Key, v => v.Select(e => e.ErrorMessage).ToArray());

                return new ValidationProblemDetails(errors)
                {
                    Status = status,
                    Title = title,
                    Detail = "One or more fields have not passed validation.",
                    Instance = httpContext.Request.Path
                    
                };

             case NotFoundException notFoundException:
                status = StatusCodes.Status404NotFound; 
                title = "Nie znaleziono zasobu";
                 detail = notFoundException.Message;
                 break;
             
            case PollInactiveException pollInactiveException:
                status = StatusCodes.Status400BadRequest;
                title = "Poll is inactive";
                detail = pollInactiveException.Message;
                break;
        }

        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
            
        };
    }
}
