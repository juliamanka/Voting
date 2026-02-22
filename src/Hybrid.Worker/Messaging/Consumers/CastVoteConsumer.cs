using System.Diagnostics;
using Hybrid.Worker.Monitoring;
using MassTransit;
using Voting.Application.DTOs;
using Voting.Domain.Entities;
using Voting.Infrastructure.Database;

namespace Hybrid.Worker.Messaging.Consumers;

public class CastVoteConsumer : IConsumer<CastVoteCommand>
{
    private readonly VotingDbContext _dbContext;
    private readonly ILogger<CastVoteConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _instanceId;

    public CastVoteConsumer(VotingDbContext dbContext, ILogger<CastVoteConsumer> logger, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
        _instanceId = _configuration.GetValue<string?>("Worker:WorkerId");
    }

    public async Task Consume(ConsumeContext<CastVoteCommand> context)
    {
        _logger.LogInformation("Received CastVoteCommand: PollId={PollId}, OptionId={OptionId}",
            context.Message.PollId, context.Message.PollOptionId);
        
        var msg = context.Message;

        var vote = new VoteRecord
        {
            VoteId = Guid.NewGuid(),
            PollId = msg.PollId,
            PollOptionId = msg.PollOptionId,
            UserId = msg.UserId
        };
        
        _dbContext.Votes.Add(vote);
        await _dbContext.SaveChangesAsync(context.CancellationToken);
        
        var duration = DateTime.UtcNow - msg.CreatedAtUtc;
        
        var tags = new TagList
        {
            { "architecture", "hybrid" },
            { "worker_instance", _instanceId }
        };

        VotingMetrics.VoteProcessingDurationSeconds.Record(duration.TotalSeconds, tags);
        // metryka vote_processing_duration_seconds = pełny czas od przyjęcia głosu przez
        // API do zakończenia przetwarzania (w tym DB) - end to end processing time
        VotingMetrics.VotesProcessed.Add(1, tags);
        
        await context.Publish(new VoteRecordedEvent(
            vote.VoteId, vote.PollId, vote.PollOptionId, vote.UserId, vote.Timestamp));
    }
}