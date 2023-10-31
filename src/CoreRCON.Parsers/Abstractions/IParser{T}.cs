namespace CoreRCON.Parsers.Abstractions;

/// <summary> Defines a parser of <typeparamref name="T"/>. </summary>
/// <typeparam name="T"> The type of <see cref="IParseable{T}"/> parsed by the implementation. </typeparam>
public interface IParser<T>
    where T : class, IParseable<T>
{
    /// <summary> Returns if the line received from the server can be parsed into the desired type. </summary>
    /// <param name="input"> Single line from the server. </param>
    bool IsMatch(string input);

    /// <summary> Parses the line from the server into the desired type. </summary>
    /// <param name="input"> Single line from the server. </param>
    T Parse(string input);
}
