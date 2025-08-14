namespace Racoon.Parsers.Abstractions;

/// <summary> Defines a parser of <typeparamref name="T"/>. </summary>
/// <typeparam name="T"> The type of <see cref="IParseable{T}"/> parsed by the implementation. </typeparam>
public interface IParser<T>
    where T : class, IParseable<T>
{
    /// <summary> Determines whether the given <paramref name="value"/> can be parsed to an instance of <typeparamref name="T"/>. </summary>
    /// <param name="value"> A text value to match. </param>
    public bool IsMatch( string value );

    /// <summary> Parses the given <paramref name="value"/> to an instance of <typeparamref name="T"/>. </summary>
    /// <param name="value"> The text value to be parsed. </param>
    public T Parse( string value );
}
