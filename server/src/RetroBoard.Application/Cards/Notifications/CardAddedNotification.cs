using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Cards.Notifications;

public record CardAddedNotification(string Slug, CardDto Card) : INotification;
