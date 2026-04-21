using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Voting.Application.Interfaces;
using Voting.Application.Services;

namespace Voting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IVotingService, VotingService>();
        services.AddScoped<IVoteWriteService, VoteWriteService>();
        services.AddScoped<IVoteProjectionAndAuditService, VoteProjectionAndAuditService>();
        services.AddScoped<IPollService, PollService>();
        services.AddScoped<IVoteValidationService, VoteValidationService>();
        services.AddScoped<IEligibilityService, EligibilityService>();

        return services;
    }
}
