using FluentAssertions;
using RetroBoard.Application.Boards.Commands.ImportBoard;
using RetroBoard.Application.Tests.TestSupport;
using Xunit;

namespace RetroBoard.Application.Tests.Boards;

public class ImportBoardCommandHandlerTests
{
    [Fact]
    public async Task Imports_board_with_columns_and_cards()
    {
        var db = TestDb.NewInMemory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-25T10:00:00Z"));
        var handler = new ImportBoardCommandHandler(db, clock);

        var dto = await handler.Handle(new ImportBoardCommand(
            "Imported",
            new[] { "Plus", "Minus" },
            new[]
            {
                new ImportedCard("Yay", "Alice", 0, 3),
                new ImportedCard("Boo", "", 1, 0),
            }), default);

        dto.Slug.Should().Be("imported");
        dto.Columns.Should().HaveCount(2);
        dto.Cards.Should().HaveCount(2);
        dto.Cards.Single(c => c.ColumnIndex == 1).Author.Should().Be("Anonymous");
    }
}
