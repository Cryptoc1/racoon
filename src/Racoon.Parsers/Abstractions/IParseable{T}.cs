namespace Racoon.Parsers.Abstractions;

/// <summary> Defines a type that can be parsed by an <see cref="IParser{T}"/>. </summary>
/// <typeparam name="T"> The type implementing this interface. </typeparam>
public interface IParseable<T> where T : class, IParseable<T>;
