using RetroBoard.Application.Common.Abstractions;

namespace RetroBoard.Application.Tests.TestSupport;

public class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = start;
    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
}
