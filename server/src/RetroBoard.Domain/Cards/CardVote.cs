namespace RetroBoard.Domain.Cards;

public class CardVote
{
    public Guid CardId { get; set; }
    public string SessionId { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
