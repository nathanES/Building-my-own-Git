using codecrafters_git.Utils;

namespace codecrafters_git.ResultPattern;

public sealed class Error(string Code, string? Description = null) : Comparable
{
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Code;
    }
}