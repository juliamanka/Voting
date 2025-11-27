using System.ComponentModel.DataAnnotations;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Voting.Application.DTOs;
using Voting.Domain.Entities;
using Voting.Infrastructure.Database;

namespace AsynchronousVoting.Worker.Messaging.Consumers;

public class CastVoteConsumer : IConsumer<CastVoteCommand>
{
    private readonly VotingDbContext _dbContext;

    public CastVoteConsumer(VotingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<CastVoteCommand> context)
    {
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

        await context.Publish(new VoteRecordedEvent(
            vote.VoteId, vote.PollId, vote.PollOptionId, vote.UserId, vote.Timestamp));
    }
}