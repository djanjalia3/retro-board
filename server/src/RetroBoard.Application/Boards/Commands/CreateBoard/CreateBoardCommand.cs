using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Commands.CreateBoard;

public record CreateBoardCommand(string Name, IReadOnlyList<string>? Columns) : IRequest<BoardDto>;
