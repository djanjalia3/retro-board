using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Boards.Commands.ImportBoard;

public record ImportedCard(string Text, string Author, int ColumnIndex, int Votes);

public record ImportBoardCommand(
    string Name,
    IReadOnlyList<string> Columns,
    IReadOnlyList<ImportedCard> Cards) : IRequest<BoardDto>;
