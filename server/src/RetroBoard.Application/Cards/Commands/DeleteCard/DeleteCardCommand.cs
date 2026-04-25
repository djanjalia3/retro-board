using MediatR;

namespace RetroBoard.Application.Cards.Commands.DeleteCard;

public record DeleteCardCommand(string Slug, Guid CardId) : IRequest<Unit>;
