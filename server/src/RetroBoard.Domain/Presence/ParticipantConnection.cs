namespace RetroBoard.Domain.Presence;

public class ParticipantConnection
{
    public long ParticipantId { get; set; }
    public string ConnectionId { get; set; } = default!;
    public string SessionId { get; set; } = default!;
    public DateTimeOffset ConnectedAt { get; set; }
}
