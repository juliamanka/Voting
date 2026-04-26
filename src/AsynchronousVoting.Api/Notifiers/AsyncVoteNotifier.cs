using MassTransit;
using Microsoft.AspNetCore.Http;
using Voting.Api.Common.RequestTiming;
using Voting.Application.DTOs;
using Voting.Application.Interfaces;
using Voting.Application.Messaging;
using Voting.Domain.Entities;
using Voting.Domain.Enums;
using Voting.Infrastructure.Database;

namespace AsynchronousVoting.Api.Notifiers;

public class AsyncVoteNotifier : IVoteNotifier
{
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly VotingDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AsyncVoteNotifier> _logger;

    public AsyncVoteNotifier(
        ISendEndpointProvider sendEndpointProvider,
        VotingDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AsyncVoteNotifier> logger)
    {
        _sendEndpointProvider = sendEndpointProvider;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Guid> NotifyVoteAsync(Guid pollId, Guid optionId, string? userId, CancellationToken ct)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var requestStartedAtUtc = RequestTimingContext.GetRequestStartedAtUtc(
                _httpContextAccessor.HttpContext,
                DateTime.UtcNow);
            var acceptedAtUtc = DateTime.UtcNow;
            var submissionId = Guid.NewGuid();
            var command = new CastVoteCommand(
                submissionId,
                pollId,
                optionId,
                userId,
                requestStartedAtUtc,
                acceptedAtUtc);
            var submission = new VoteSubmission
            {
                SubmissionId = submissionId,
                PollId = pollId,
                PollOptionId = optionId,
                UserId = userId,
                Architecture = "async",
                Status = VoteStatus.Pending,
                RequestStartedAtUtc = requestStartedAtUtc,
                AcceptedAtUtc = acceptedAtUtc,
                HttpResponseLatencyMs = Math.Max(0L, (long)(acceptedAtUtc - requestStartedAtUtc).TotalMilliseconds)
            };

            await _dbContext.VoteSubmissions.AddAsync(submission, ct);

            var endpoint = await _sendEndpointProvider.GetSendEndpoint(
                new Uri($"queue:{VoteQueueNames.AsyncCastVoteQueue}"));
            await endpoint.Send(
                command,
                sendContext =>
                {
                    sendContext.Headers.Set("submission-id", submissionId.ToString("D"));
                    sendContext.Headers.Set("request-started-at-utc", requestStartedAtUtc.ToString("O"));
                    sendContext.Headers.Set("api-accepted-at-utc", acceptedAtUtc.ToString("O"));
                },
                ct);

            await _dbContext.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Vote submission {SubmissionId} persisted and async command scheduled for poll {PollId}.",
                submissionId,
                pollId);

            return submissionId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Transaction failed for Poll {PollId}", pollId);
            throw;
        }
    }
}
