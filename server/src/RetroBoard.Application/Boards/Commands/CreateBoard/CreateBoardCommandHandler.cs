using MediatR;
using Microsoft.EntityFrameworkCore;
using RetroBoard.Application.Common.Abstractions;
using RetroBoard.Application.Common.Dtos;
using RetroBoard.Application.Common.Exceptions;
using RetroBoard.Domain.Boards;
using RetroBoard.Domain.Common;

namespace RetroBoard.Application.Boards.Commands.CreateBoard;

public class CreateBoardCommandHandler(IBoardDbContext db, IClock clock)
    : IRequestHandler<CreateBoardCommand, BoardDto>
{
    public async Task<BoardDto> Handle(CreateBoardCommand cmd, CancellationToken ct)
    {
        var slug = Slug.Create(cmd.Name);
        if (await db.Boards.AnyAsync(b => b.Slug == slug, ct))
            throw new ConflictException("Board name already taken, choose another.");

        var titles = (cmd.Columns is { Count: > 0 } ? cmd.Columns : DefaultColumns.Titles).ToList();
        var board = new Board
        {
            Slug = slug,
            Name = cmd.Name,
            CreatedAt = clock.UtcNow,
            Columns = titles.Select((t, i) => new BoardColumn { Position = i, Title = t }).ToList(),
        };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);

        return new BoardDto(
            board.Id, board.Slug, board.Name, board.CreatedAt,
            board.Columns
                .OrderBy(c => c.Position)
                .Select(c => new ColumnDto(c.Id, c.Position, c.Title))
                .ToList(),
            Array.Empty<CardDto>());
    }
}
