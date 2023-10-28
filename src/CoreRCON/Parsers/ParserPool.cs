using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CoreRCON.Parsers;

/// <summary> Provides access to a pool of <see cref="IParser{T}"/>s </summary>
public class ParserPool
{
    public static readonly ParserPool Shared = new();

    private readonly ConcurrentDictionary<Type, object> _parserByParsableType = [];

    /// <summary> Get a parser of type <typeparamref name="T"/> from the pool. </summary>
    /// <typeparam name="T"> The type of parsable object. </typeparam>
    public virtual IParser<T> Get<T>()
        where T : class, IParseable<T>
    {
        var parser = _parserByParsableType.GetOrAdd(
            typeof(T),
            t => Activator.CreateInstance(FindParserImplementation<T>(t.Assembly)));

        // NOTE: avoid runtime checks; we know this must be an `IParser<T>`
        return Unsafe.As<IParser<T>>(parser);
    }

    /// <summary> Scans the given <paramref name="assembly"/> for the first implementation of an <see cref="IParser{T}"/>. </summary>
    /// <typeparam name="T"> The type of <see cref="IParseable"/> to find a parser for. </typeparam>
    /// <param name="assembly"> The assembly to search. </param>
    /// <returns> The implementation type for an <see cref="IParser{T}"/> of <typeparamref name="T"/>. </returns>
    /// <exception cref="ArgumentException"> A parser could not be found. </exception>
    public static Type FindParserImplementation<T>(Assembly assembly)
        where T : class, IParseable<T>
    {
        var parserType = typeof(IParser<T>);
        foreach (var type in assembly.GetTypes())
        {
            if (type.GetInterfaces().Contains(parserType))
            {
                return type;
            }
        }

        throw new ArgumentException($"An implementation of '{parserType.Name}' was not found in the assembly '{assembly.FullName}'.", nameof(assembly));
    }
}
