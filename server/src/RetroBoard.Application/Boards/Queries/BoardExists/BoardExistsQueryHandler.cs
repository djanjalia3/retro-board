using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;

namespace RetroBoard.Application.Boards.Queries.BoardExists;

public class BoardExistsQueryHandler(IBoardDbContext db) : IRequestHandler<BoardExistsQuery, bool>
{
    public Task<bool> Handle(BoardExistsQuery q, CancellationToken ct) =>
        db.Boards.AsNoTracking().AnyAsync(b => b.Slug == q.Slug, ct);
}
