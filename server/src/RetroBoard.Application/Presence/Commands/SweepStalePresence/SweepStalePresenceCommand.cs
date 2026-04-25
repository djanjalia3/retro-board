using MediatR;

namespace RetroBoard.Application.Presence.Commands.SweepStalePresence;

public record SweepStalePresenceCommand(TimeSpan StaleAfter) : IRequest<Unit>;
