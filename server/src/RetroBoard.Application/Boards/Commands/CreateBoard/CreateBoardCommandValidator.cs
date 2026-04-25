using FluentValidation;

namespace RetroBoard.Application.Boards.Commands.CreateBoard;

public class CreateBoardCommandValidator : AbstractValidator<CreateBoardCommand>
{
    public CreateBoardCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleForEach(x => x.Columns).NotEmpty().MaximumLength(80)
            .When(x => x.Columns is not null);
        RuleFor(x => x.Columns).Must(c => c is null || c.Count <= 12)
            .WithMessage("at most 12 columns");
    }
}
