using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Api.Hubs;
using RetroBoard.Application.Cards.Notifications;

namespace RetroBoard.Api.Realtime;

public class CardAddedNotificationHandler(IHubContext<BoardHub, IBoardHubClient> hub)
    : INotificationHandler<CardAddedNotification>
{
    public Task Handle(CardAddedNotification n, CancellationToken ct) =>
        hub.Clients.Group(BoardHub.GroupName(n.Slug)).CardAdded(n.Card);
}
