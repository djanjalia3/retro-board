namespace RetroBoard.Application.Common.Dtos;

public record BoardSummaryDto(long Id, string Slug, string Name, DateTimeOffset CreatedAt);
