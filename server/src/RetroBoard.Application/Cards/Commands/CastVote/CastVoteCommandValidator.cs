using FluentValidation;

namespace RetroBoard.Application.Cards.Commands.CastVote;

public class CastVoteCommandValidator : AbstractValidator<CastVoteCommand>
{
    public CastVoteCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty();
        RuleFor(x => x.CardId).NotEqual(Guid.Empty);
        RuleFor(x => x.SessionId).NotEmpty().MaximumLength(120);
    }
}
