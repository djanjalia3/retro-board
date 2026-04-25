using MediatR;

namespace RetroBoard.Application.Presence.Commands.RefreshPresence;

public record RefreshPresenceCommand(string ConnectionId) : IRequest<Unit>;
