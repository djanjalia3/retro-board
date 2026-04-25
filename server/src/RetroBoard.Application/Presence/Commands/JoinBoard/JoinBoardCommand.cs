using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Presence.Commands.JoinBoard;

public record JoinBoardResult(IReadOnlyList<ParticipantDto> Participants);

public record JoinBoardCommand(string Slug, string ConnectionId, string SessionId, string DisplayName)
    : IRequest<JoinBoardResult>;
