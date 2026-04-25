using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Queries.GetBoard;

public class GetBoardQueryHandler(IBoardDbContext db) : IRequestHandler<GetBoardQuery, BoardDto?>
{
    public async Task<BoardDto?> Handle(GetBoardQuery q, CancellationToken ct)
    {
        var board = await db.Boards
            .AsNoTracking()
            .Include(b => b.Columns)
            .Include(b => b.Cards).ThenInclude(c => c.Votes)
            .FirstOrDefaultAsync(b => b.Slug == q.Slug, ct);
        if (board is null) return null;

        var columns = board.Columns.OrderBy(c => c.Position).ToList();
        var positionByColumnId = columns.ToDictionary(c => c.Id, c => c.Position);

        return new BoardDto(
            board.Id, board.Slug, board.Name, board.CreatedAt,
            columns.Select(c => new ColumnDto(c.Id, c.Position, c.Title)).ToList(),
            board.Cards
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CardDto(
                    c.Id, c.ColumnId,
                    positionByColumnId.TryGetValue(c.ColumnId, out var p) ? p : -1,
                    c.Text, c.Author, c.CreatedAt, c.Votes.Count))
                .ToList());
    }
}
