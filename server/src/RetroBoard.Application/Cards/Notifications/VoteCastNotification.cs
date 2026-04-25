using MediatR;

namespace RetroBoard.Application.Cards.Notifications;

public record VoteCastNotification(string Slug, Guid CardId, int Votes, string SessionId) : INotification;
