using RetroBoard.Application.Common.Abstractions;

namespace RetroBoard.Infrastructure.Time;

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
