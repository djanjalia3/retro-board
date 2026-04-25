namespace RetroBoard.Domain.Cards;

public class Card
{
    public Guid Id { get; set; }
    public long BoardId { get; set; }
    public long ColumnId { get; set; }
    public string Text { get; set; } = default!;
    public string Author { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }

    public List<CardVote> Votes { get; set; } = new();
}
