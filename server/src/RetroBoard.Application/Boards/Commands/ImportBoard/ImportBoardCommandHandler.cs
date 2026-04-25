using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Domain.Boards;
using RetroBoard.Domain.Cards;
using RetroBoard.Domain.Common;

namespace RetroBoard.Application.Boards.Commands.ImportBoard;

public class ImportBoardCommandHandler(IBoardDbContext db, IClock clock)
    : IRequestHandler<ImportBoardCommand, BoardDto>
{
    public async Task<BoardDto> Handle(ImportBoardCommand cmd, CancellationToken ct)
    {
        var slug = Slug.Create(cmd.Name);
        if (await db.Boards.AnyAsync(b => b.Slug == slug, ct))
            throw new ConflictException("Board name already taken, choose another.");

        var columns = cmd.Columns.Select((t, i) => new BoardColumn { Position = i, Title = t }).ToList();
        var board = new Board
        {
            Slug = slug,
            Name = cmd.Name,
            CreatedAt = clock.UtcNow,
            Columns = columns,
        };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);  // assigns column IDs

        var cards = new List<Card>();
        foreach (var c in cmd.Cards)
        {
            if (c.ColumnIndex < 0 || c.ColumnIndex >= columns.Count) continue;
            var card = new Card
            {
                Id = Guid.NewGuid(),
                BoardId = board.Id,
                ColumnId = columns[c.ColumnIndex].Id,
                Text = c.Text,
                Author = string.IsNullOrWhiteSpace(c.Author) ? "Anonymous" : c.Author,
                CreatedAt = clock.UtcNow,
            };
            for (var v = 0; v < c.Votes; v++)
                card.Votes.Add(new CardVote { CardId = card.Id, SessionId = $"import-{Guid.NewGuid()}", CreatedAt = clock.UtcNow });
            cards.Add(card);
        }
        db.Cards.AddRange(cards);
        await db.SaveChangesAsync(ct);

        var positionByColumnId = columns.ToDictionary(c => c.Id, c => c.Position);
        return new BoardDto(
            board.Id, board.Slug, board.Name, board.CreatedAt,
            columns.Select(c => new ColumnDto(c.Id, c.Position, c.Title)).ToList(),
            cards.Select(c => new CardDto(c.Id, c.ColumnId, positionByColumnId[c.ColumnId],
                c.Text, c.Author, c.CreatedAt, c.Votes.Count)).ToList());
    }
}
