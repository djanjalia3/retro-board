using Microsoft.EntityFrameworkCore;
using RetroBoard.Infrastructure.Persistence;

namespace RetroBoard.Application.Tests.TestSupport;

public static class TestDb
{
    public static BoardDbContext NewInMemory()
    {
        var opts = new DbContextOptionsBuilder<BoardDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BoardDbContext(opts);
    }
}
