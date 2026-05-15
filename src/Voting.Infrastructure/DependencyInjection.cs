using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Voting.Domain.Repository;
using Voting.Infrastructure.Database;
using Voting.Infrastructure.Repositories;

namespace Voting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        void ConfigureOptions(DbContextOptionsBuilder options)
        {
            options.UseNpgsql(connectionString);
        }

        services.AddDbContext<VotingDbContext>(ConfigureOptions);
        
        services.AddScoped<IVoteRepository, VoteRepository>();
        services.AddScoped<IPollRepository, PollRepository>();
        services.AddScoped<IPollResultsProjectionRepository, PollResultsProjectionRepository>();

        return services;
    }
}
