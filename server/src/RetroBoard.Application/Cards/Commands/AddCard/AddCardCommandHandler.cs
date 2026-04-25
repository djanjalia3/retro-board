using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Cards.Notifications;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Domain.Cards;

namespace RetroBoard.Application.Cards.Commands.AddCard;

public class AddCardCommandHandler(IBoardDbContext db, IClock clock, IPublisher publisher)
    : IRequestHandler<AddCardCommand, CardDto>
{
    public async Task<CardDto> Handle(AddCardCommand cmd, CancellationToken ct)
    {
        var board = await db.Boards
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.Slug == cmd.Slug, ct)
            ?? throw new NotFoundException($"Board '{cmd.Slug}' not found");
        var column = board.Columns.FirstOrDefault(c => c.Position == cmd.ColumnIndex)
            ?? throw new NotFoundException($"Column index {cmd.ColumnIndex} out of range");

        var card = new Card
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            ColumnId = column.Id,
            Text = cmd.Text,
            Author = cmd.Author,
            CreatedAt = clock.UtcNow,
        };
        db.Cards.Add(card);
        await db.SaveChangesAsync(ct);

        var dto = new CardDto(card.Id, card.ColumnId, column.Position,
            card.Text, card.Author, card.CreatedAt, 0);
        await publisher.Publish(new CardAddedNotification(cmd.Slug, dto), ct);
        return dto;
    }
}
