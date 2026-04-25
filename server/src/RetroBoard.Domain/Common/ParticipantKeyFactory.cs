namespace RetroBoard.Domain.Common;

public static class ParticipantKeyFactory
{
    public static string Create(string displayName)
    {
        var input = displayName ?? string.Empty;
        try
        {
            return Slug.Create(input);
        }
        catch (ArgumentException)
        {
            return $"anon-{input.Length}";
        }
    }
}
