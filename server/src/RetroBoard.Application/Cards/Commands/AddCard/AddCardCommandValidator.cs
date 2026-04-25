using FluentValidation;

namespace RetroBoard.Application.Cards.Commands.AddCard;

public class AddCardCommandValidator : AbstractValidator<AddCardCommand>
{
    public AddCardCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Author).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ColumnIndex).GreaterThanOrEqualTo(0);
    }
}
