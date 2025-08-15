namespace Racoon.Parsers.Abstractions;

/// <summary> Defines a type that represents parsed data. </summary>
/// <typeparam name="T"> The type implementing this interface. </typeparam>
public interface IParsed<T> where T : class, IParsed<T>;
