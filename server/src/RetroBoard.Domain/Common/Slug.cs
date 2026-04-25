using System.Text.RegularExpressions;

namespace RetroBoard.Domain.Common;

public static class Slug
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Invalid = new(@"[^a-z0-9-]", RegexOptions.Compiled);
    private static readonly Regex MultiHyphen = new(@"-+", RegexOptions.Compiled);

    public static string Create(string name)
    {
        var s = (name ?? string.Empty).Trim().ToLowerInvariant();
        s = Whitespace.Replace(s, "-");
        s = Invalid.Replace(s, "");
        s = MultiHyphen.Replace(s, "-").Trim('-');
        if (string.IsNullOrEmpty(s))
            throw new ArgumentException("invalid board name", nameof(name));
        return s;
    }
}
