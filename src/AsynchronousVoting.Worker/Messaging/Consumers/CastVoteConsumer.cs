using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using AsynchronousVoting.Worker.Monitoring;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Voting.Application.DTOs;
using Voting.Domain.Entities;
using Voting.Infrastructure.Database;

namespace AsynchronousVoting.Worker.Messaging.Consumers;

public class CastVoteConsumer : IConsumer<CastVoteCommand>
{
    private readonly VotingDbContext _dbContext;
    private readonly ILogger<CastVoteConsumer> _logger;

    public CastVoteConsumer(VotingDbContext dbContext, ILogger<CastVoteConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CastVoteCommand> context)
    {
        _logger.LogInformation("Received CastVoteCommand: PollId={PollId}, OptionId={OptionId}",
            context.Message.PollId, context.Message.PollOptionId);
        
        var msg = context.Message;
        
         var optionExists = await _dbContext.PollOptions
             .AnyAsync(o => o.PollId == msg.PollId && o.PollOptionId == msg.PollOptionId,
                 context.CancellationToken);

         if (!optionExists)
         {
             throw new ValidationException("Chosen answer doesn't exist in the poll.");
         }

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
            { "architecture", "async" }
        };

        VotingMetrics.VoteProcessingDurationSeconds.Record(duration.TotalSeconds, tags);
        // metryka vote_processing_duration_seconds = pełny czas od przyjęcia głosu przez
        // API do zakończenia przetwarzania (w tym DB) - end to end processing time
        
        await context.Publish(new VoteRecordedEvent(
            vote.VoteId, vote.PollId, vote.PollOptionId, vote.UserId, vote.Timestamp));
    }
}