using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Queries.ListBoards;

public class ListBoardsQueryHandler(IBoardDbContext db)
    : IRequestHandler<ListBoardsQuery, IReadOnlyList<BoardSummaryDto>>
{
    public async Task<IReadOnlyList<BoardSummaryDto>> Handle(ListBoardsQuery q, CancellationToken ct)
    {
        return await db.Boards
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BoardSummaryDto(b.Id, b.Slug, b.Name, b.CreatedAt))
            .ToListAsync(ct);
    }
}
