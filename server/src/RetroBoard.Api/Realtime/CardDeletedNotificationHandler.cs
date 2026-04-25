using MediatR;
using Microsoft.AspNetCore.SignalR;
using RetroBoard.Api.Hubs;
using RetroBoard.Application.Cards.Notifications;

namespace RetroBoard.Api.Realtime;

public class CardDeletedNotificationHandler(IHubContext<BoardHub, IBoardHubClient> hub)
    : INotificationHandler<CardDeletedNotification>
{
    public Task Handle(CardDeletedNotification n, CancellationToken ct) =>
        hub.Clients.Group(BoardHub.GroupName(n.Slug)).CardDeleted(n.CardId);
}
