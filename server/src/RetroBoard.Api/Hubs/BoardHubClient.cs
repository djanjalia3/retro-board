using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Api.Hubs;

public interface IBoardHubClient
{
    Task CardAdded(CardDto card);
    Task CardDeleted(Guid cardId);
    Task VoteCast(Guid cardId, int votes, string sessionId);
    Task PresenceChanged(IReadOnlyList<ParticipantDto> participants);
}
