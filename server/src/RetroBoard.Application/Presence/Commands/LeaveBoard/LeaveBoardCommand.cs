using MediatR;

namespace RetroBoard.Application.Presence.Commands.LeaveBoard;

public record LeaveBoardCommand(string Slug, string ConnectionId) : IRequest<Unit>;
