using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Racoon.Parsers.Abstractions;

namespace Racoon.Parsers;

/// <summary> Defines a type that provides reusable instances <see cref="IParser{T}"/>s. </summary>
[method: DynamicDependency( DynamicallyAccessedMemberTypes.All, typeof( IParser<> ) )]
public class ParserPool( )
{
    /// <summary> A reference to a shared instance of <see cref="ParserPool"/>. </summary>
    public static readonly ParserPool Shared = new();

    private readonly ConcurrentDictionary<Type, object> parserByParsableType = [];

    /// <summary> Get a parser of type <typeparamref name="T"/> from the pool. </summary>
    /// <typeparam name="T"> The type of parsable object. </typeparam>
    [RequiresUnreferencedCode( "Parser types may be removed from the Assembly when trimming is enabled." )]
    public virtual IParser<T> Get<T>( )
        where T : class, IParseable<T>
    {
        var parser = parserByParsableType.GetOrAdd(
            typeof( T ),
            type => CreateInstance( this, type )! );

        // NOTE: avoid runtime checks; we know this must be an `IParser<T>`
        return Unsafe.As<IParser<T>>( parser );

        static object? CreateInstance( ParserPool pool, Type type )
        {
            var parser = FindImplementations<T>( type.Assembly ).Single();
            if( (parser.GetConstructor( BindingFlags.Instance | BindingFlags.Public, [ typeof( ParserPool ) ] ) ?? parser.GetConstructor( BindingFlags.Instance | BindingFlags.Public, [ pool.GetType() ] )) is not null )
            {
                return Activator.CreateInstance( parser, pool );
            }

            return Activator.CreateInstance( parser );
        }
    }

    /// <summary> Searches the given <paramref name="assembly"/> for implementations of <see cref="IParser{T}"/>. </summary>
    /// <typeparam name="T"> The type of <see cref="IParseable{T}"/> to find a parser for. </typeparam>
    /// <param name="assembly"> The assembly to search. </param>
    [DynamicDependency( DynamicallyAccessedMemberTypes.All, typeof( IParser<> ) )]
    [RequiresUnreferencedCode( "Parser types may be removed from the Assembly when trimming is enabled." )]
    public static IEnumerable<Type> FindImplementations<T>( Assembly assembly )
        where T : class, IParseable<T>
    {
        Type? parserType = null;
        foreach( var type in assembly.GetTypes().Where( IsCandidate ) )
        {
            if( type.GetInterfaces().Contains( parserType ??= typeof( IParser<T> ) ) )
            {
                yield return type;
            }
        }

        static bool IsCandidate( [DynamicallyAccessedMembers( DynamicallyAccessedMemberTypes.PublicConstructors )] Type type ) => !(!type.IsPublic || !type.IsClass || type.IsAbstract || type.IsGenericType);
    }
}
