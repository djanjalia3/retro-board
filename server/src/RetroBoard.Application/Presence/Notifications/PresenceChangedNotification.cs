using MediatR;
using RetroBoard.Application.Common.Dtos;

namespace RetroBoard.Application.Presence.Notifications;

public record PresenceChangedNotification(string Slug, IReadOnlyList<ParticipantDto> Participants) : INotification;
