using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Api.Hubs;
using RetroBoard.Application.Presence.Notifications;

namespace RetroBoard.Api.Realtime;

public class PresenceChangedNotificationHandler(IHubContext<BoardHub, IBoardHubClient> hub)
    : INotificationHandler<PresenceChangedNotification>
{
    public Task Handle(PresenceChangedNotification n, CancellationToken ct) =>
        hub.Clients.Group(BoardHub.GroupName(n.Slug)).PresenceChanged(n.Participants);
}
