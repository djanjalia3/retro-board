using FluentValidation;

namespace RetroBoard.Application.Boards.Commands.ImportBoard;

public class ImportBoardCommandValidator : AbstractValidator<ImportBoardCommand>
{
    public ImportBoardCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Columns).NotEmpty();
        RuleForEach(x => x.Columns).NotEmpty().MaximumLength(80);
        RuleForEach(x => x.Cards).ChildRules(c =>
        {
            c.RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
            c.RuleFor(x => x.ColumnIndex).GreaterThanOrEqualTo(0);
            c.RuleFor(x => x.Votes).GreaterThanOrEqualTo(0);
        });
    }
}
