namespace RetroBoard.Application.Common.Dtos;

public record ParticipantDto(
    string ParticipantKey,
    string DisplayName,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt,
    int ConnectionCount);
