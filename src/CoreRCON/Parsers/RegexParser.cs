using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CoreRCON.Parsers;

/// <summary> Base implementation of an <see cref="IParser{T}"/> that parses input via regex. </summary>
public abstract class RegexParser<T> : IParser<T>
    where T : class, IParseable<T>
{
    [StringSyntax(StringSyntaxAttribute.Regex)]
    public string Pattern { get; }

    private Regex? pattern;
    public Regex PatternRegex => pattern ??= new(Pattern, RegexOptions.Compiled | RegexOptions.Singleline);

    protected RegexParser([StringSyntax(StringSyntaxAttribute.Regex)] string pattern)
    {
        Pattern = pattern;
    }

    public bool IsMatch(string input) => PatternRegex.IsMatch(input);

    protected abstract T Load(GroupCollection groups);

    /// <summary> Shorthand for <see cref="Parse(string)"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Parse(Group group) => Parse(group.Value);

    public T Parse(string input)
    {
        var groups = PatternRegex.Match(input).Groups;
        return Load(groups);
    }
}
