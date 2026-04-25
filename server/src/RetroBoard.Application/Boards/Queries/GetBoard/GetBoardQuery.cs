using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Queries.GetBoard;

public record GetBoardQuery(string Slug) : IRequest<BoardDto?>;
