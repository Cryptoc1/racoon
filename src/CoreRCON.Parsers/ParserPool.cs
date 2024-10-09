using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using CoreRCON.Parsers.Abstractions;

namespace CoreRCON.Parsers;

/// <summary> Defines a type that provides reusable instances <see cref="IParser{T}"/>s. </summary>
public class ParserPool
{
    /// <summary> A reference to a shared instance of <see cref="ParserPool"/>. </summary>
    public static readonly ParserPool Shared = new();

    private readonly ConcurrentDictionary<Type, object> _parserByParsableType = [];

    /// <summary> Get a parser of type <typeparamref name="T"/> from the pool. </summary>
    /// <typeparam name="T"> The type of parsable object. </typeparam>
    public virtual IParser<T> Get<T>()
        where T : class, IParseable<T>
    {
        var parser = _parserByParsableType.GetOrAdd(
            typeof(T),
            t => Activator.CreateInstance(FindImplementations<T>(t.Assembly).Single())!);

        // NOTE: avoid runtime checks; we know this must be an `IParser<T>`
        return Unsafe.As<IParser<T>>(parser);
    }

    /// <summary> Searches the given <paramref name="assembly"/> for implementations of <see cref="IParser{T}"/>. </summary>
    /// <typeparam name="T"> The type of <see cref="IParseable{T}"/> to find a parser for. </typeparam>
    /// <param name="assembly"> The assembly to search. </param>
    public static IEnumerable<Type> FindImplementations<T>(Assembly assembly)
        where T : class, IParseable<T>
    {
        Type? parserType = null;
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsPublic || !type.IsClass || type.IsAbstract || type.IsGenericType)
            {
                continue;
            }

            if (type.GetInterfaces().Contains(parserType ??= typeof(IParser<T>)))
            {
                yield return type;
            }
        }
    }
}
