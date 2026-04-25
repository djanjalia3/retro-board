using RetroBoard.Domain.Cards;
using RetroBoard.Domain.Presence;

namespace RetroBoard.Domain.Boards;

public class Board
{
    public long Id { get; set; }
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }

    public List<BoardColumn> Columns { get; set; } = new();
    public List<Card> Cards { get; set; } = new();
    public List<Participant> Participants { get; set; } = new();
}
