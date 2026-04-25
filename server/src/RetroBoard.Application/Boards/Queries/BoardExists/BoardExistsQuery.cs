using MediatR;

namespace RetroBoard.Application.Boards.Queries.BoardExists;

public record BoardExistsQuery(string Slug) : IRequest<bool>;
