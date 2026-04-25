using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Exceptions;

namespace RetroBoard.Application.Cards.Commands.DeleteCard;

public class DeleteCardCommandHandler(IBoardDbContext db, IPublisher publisher)
    : IRequestHandler<DeleteCardCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCardCommand cmd, CancellationToken ct)
    {
        var card = await db.Cards.FirstOrDefaultAsync(
            c => c.Id == cmd.CardId && c.BoardId == db.Boards
                .Where(b => b.Slug == cmd.Slug).Select(b => b.Id).FirstOrDefault(), ct)
            ?? throw new NotFoundException($"Card {cmd.CardId} not found on board '{cmd.Slug}'");
        db.Cards.Remove(card);
        await db.SaveChangesAsync(ct);
        await publisher.Publish(new CardDeletedNotification(cmd.Slug, cmd.CardId), ct);
        return Unit.Value;
    }
}
