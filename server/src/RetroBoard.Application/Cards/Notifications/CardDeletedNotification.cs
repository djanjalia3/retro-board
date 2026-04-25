using MediatR;

namespace RetroBoard.Application.Cards.Notifications;

public record CardDeletedNotification(string Slug, Guid CardId) : INotification;
