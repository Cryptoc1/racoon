using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace CoreRCON.Parsers.Abstractions;

/// <summary> Base implementation of an <see cref="IParser{T}"/> that parses input via regex. </summary>
public abstract class RegexParser<T>([StringSyntax(StringSyntaxAttribute.Regex)] string pattern) : IParser<T>
    where T : class, IParseable<T>
{
    /// <summary> The string value of the regex pattern this <see cref="IParser{T}"/> matches. </summary>
    [StringSyntax(StringSyntaxAttribute.Regex)]
    public string Pattern { get; } = pattern;

    private Regex? pattern;

    /// <summary> The compiled <see cref="Regex"/> pattern. </summary>
    public Regex PatternRegex => pattern ??= new(Pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <inheritdoc/>
    public bool IsMatch(string value)
    {
        return PatternRegex.IsMatch(value);
    }

    /// <summary> Convert the groups matched by the underlying regex pattern to an instance of <typeparamref name="T"/>. </summary>
    /// <param name="groups"> The groups that matched the parser's <see cref="Pattern" />. </param>
    protected abstract T Convert(GroupCollection groups);

    /// <inheritdoc/>
    public T Parse(string value)
    {
        var groups = PatternRegex.Match(value).Groups;
        return Convert(groups);
    }
}
