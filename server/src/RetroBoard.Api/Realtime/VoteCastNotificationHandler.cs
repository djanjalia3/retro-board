using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Api.Hubs;
using RetroBoard.Application.Cards.Notifications;

namespace RetroBoard.Api.Realtime;

public class VoteCastNotificationHandler(IHubContext<BoardHub, IBoardHubClient> hub)
    : INotificationHandler<VoteCastNotification>
{
    public Task Handle(VoteCastNotification n, CancellationToken ct) =>
        hub.Clients.Group(BoardHub.GroupName(n.Slug)).VoteCast(n.CardId, n.Votes, n.SessionId);
}
