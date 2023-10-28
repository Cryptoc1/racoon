namespace CoreRCON.Parsers;

public interface IParseable<T>
    where T : class, IParseable<T>
{
}

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
