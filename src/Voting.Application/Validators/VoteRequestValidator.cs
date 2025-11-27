using FluentValidation;
using Voting.Application.DTOs;

namespace Voting.Application.Validators;

public class VoteRequestValidator : AbstractValidator<VoteRequest>
{
    public VoteRequestValidator()
    {
        RuleFor(v => v.PollId)
            .NotEmpty()
            .WithMessage("PollId cannot be empty.");

        RuleFor(v => v.PollOptionId)
            .NotEmpty()
            .WithMessage("OptionId cannot be empty.");

        RuleFor(v => v.UserId)
            .MaximumLength(256)
            .When(v => !string.IsNullOrEmpty(v.UserId)) 
            .WithMessage("UserId cannot exceed 256 characters.");
    }
}