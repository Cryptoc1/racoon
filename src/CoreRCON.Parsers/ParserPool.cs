using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
    [RequiresUnreferencedCode("Parser types may be removed from the Assembly when trimming is enabled.")]
    public virtual IParser<T> Get<T>()
        where T : class, IParseable<T>
    {
        var parser = _parserByParsableType.GetOrAdd(
            typeof(T),
            ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type) => Activator.CreateInstance(
                FindImplementations<T>(type.Assembly).Single())!);

        // NOTE: avoid runtime checks; we know this must be an `IParser<T>`
        return Unsafe.As<IParser<T>>(parser);
    }

    /// <summary> Searches the given <paramref name="assembly"/> for implementations of <see cref="IParser{T}"/>. </summary>
    /// <typeparam name="T"> The type of <see cref="IParseable{T}"/> to find a parser for. </typeparam>
    /// <param name="assembly"> The assembly to search. </param>
    [RequiresUnreferencedCode("Parser types may be removed from the Assembly when trimming is enabled.")]
    public static IEnumerable<Type> FindImplementations<T>(Assembly assembly)
        where T : class, IParseable<T>
    {
        Type? parserType = null;
        foreach (var type in assembly.GetTypes().Where(IsCandidate))
        {
            if (type.GetInterfaces().Contains(parserType ??= typeof(IParser<T>)))
            {
                yield return type;
            }
        }

        static bool IsCandidate([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type) => !(!type.IsPublic || !type.IsClass || type.IsAbstract || type.IsGenericType);
    }
}
