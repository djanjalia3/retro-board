using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Queries.ListBoards;

public record ListBoardsQuery : IRequest<IReadOnlyList<BoardSummaryDto>>;
