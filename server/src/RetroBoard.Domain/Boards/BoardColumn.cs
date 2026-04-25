namespace RetroBoard.Domain.Boards;

public class BoardColumn
{
    public long Id { get; set; }
    public long BoardId { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = default!;
}
