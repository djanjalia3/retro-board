namespace RetroBoard.Domain.Presence;

public class Participant
{
    public long Id { get; set; }
    public long BoardId { get; set; }
    public string ParticipantKey { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public List<ParticipantConnection> Connections { get; set; } = new();
}
