namespace RetroBoard.Application.Common.Dtos;

public record CardDto(
    Guid Id,
    long ColumnId,
    int ColumnIndex,
    string Text,
    string Author,
    DateTimeOffset CreatedAt,
    int Votes);
