namespace RetroBoard.Application.Common.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
