namespace RetroBoard.Application.Common.Dtos;

public record BoardDto(
    long Id,
    string Slug,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ColumnDto> Columns,
    IReadOnlyList<CardDto> Cards);
