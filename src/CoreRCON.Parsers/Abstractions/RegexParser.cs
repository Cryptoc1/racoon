using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Abstractions;

/// <summary> Base implementation of an <see cref="IParser{T}"/> that parses input via regex. </summary>
public abstract class RegexParser<T> : IParser<T>
    where T : class, IParseable<T>
{
    /// <summary> The string value of the regex pattern this <see cref="IParser{T}"/> matches. </summary>
    [StringSyntax(StringSyntaxAttribute.Regex)]
    public string Pattern { get; }

    private Regex? pattern;

    /// <summary> The compiled <see cref="Regex"/> pattern. </summary>
    public Regex PatternRegex => pattern ??= new(Pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    protected RegexParser([StringSyntax(StringSyntaxAttribute.Regex)] string pattern)
    {
        Pattern = pattern;
    }

    /// <inheritdoc/>
    public bool IsMatch(string value) => PatternRegex.IsMatch(value);

    /// <summary> Convert the groups matched by the underlying regex pattern to an instance of <typeparamref name="T"/>. </summary>
    /// <param name="groups"> The groups that matched the parser's <see cref="Pattern" />. </param>
    protected abstract T Convert(GroupCollection groups);

    /// <summary> Shorthand for <see cref="Parse(string)"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Parse(Group group) => Parse(group.Value);

    /// <inheritdoc/>
    public T Parse(string value)
    {
        var groups = PatternRegex.Match(value).Groups;
        return Convert(groups);
    }
}
