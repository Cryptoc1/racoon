using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Racoon.Parsers.Abstractions;
using Racoon.Parsers.Standard;

namespace Racoon.Parsers;

/// <summary> Defines a type that provides reusable instances <see cref="IParser{T}"/>s. </summary>
public sealed class ParserPool
{
    private readonly IReadOnlyDictionary<Type, Type> map;
    private readonly ConcurrentDictionary<Type, object> cache = [];

    private ParserPool( IReadOnlyDictionary<Type, Type> values )
    {
        map = values;
    }

    /// <summary> Create an instance of a <see cref="ParserPool"/>. </summary>
    /// <param name="configure"> A delegate to configure the pool. </param>
    public static ParserPool Create( Action<IParserPoolBuilder>? configure = default )
    {
        var builder = new ParserPoolBuilder();
        configure?.Invoke( builder );

        return new( builder );
    }

    /// <summary> Create an instance of the default <see cref="ParserPool"/>. </summary>
    /// <param name="configure"> A delegate to configure additional parsers. </param>
    /// <remarks> The "default" pool includes all <see cref="Standard"/> parsers. </remarks>
    /// <seealso cref="ParserPoolExtensions.UseStandard(IParserPoolBuilder)"/>
    public static ParserPool CreateDefault( Action<IParserPoolBuilder>? configure = default ) => Create( builder =>
    {
        ArgumentNullException.ThrowIfNull( builder );

        builder.UseStandard();
        configure?.Invoke( builder );
    } );

    /// <summary> Get a parser of type <typeparamref name="T"/> from the pool. </summary>
    /// <typeparam name="T"> The type of parsable object. </typeparam>
    public IParser<T> Get<T>( )
        where T : class, IParsed<T>
    {
        var parser = cache.GetOrAdd(
            typeof( T ),
            type => CreateInstance( this, type )! );

        // NOTE: avoid runtime checks; we know this must be an `IParser<T>`
        return Unsafe.As<IParser<T>>( parser );

        static object? CreateInstance( ParserPool pool, Type type )
        {
            if( pool.map.TryGetValue( type, out var parser ) )
            {
                if( parser.GetConstructor( BindingFlags.Instance | BindingFlags.Public, [ typeof( ParserPool ) ] ) is not null )
                {
                    return Activator.CreateInstance( parser, pool );
                }

                return Activator.CreateInstance( parser );
            }

            return default;
        }
    }
}

sealed file class ParserPoolBuilder( ) : IParserPoolBuilder, IReadOnlyDictionary<Type, Type>
{
    private readonly Dictionary<Type, Type> parsers = [];

    public Type this[ Type key ] => parsers[ key ];

    public IEnumerable<Type> Keys => parsers.Keys;
    public IEnumerable<Type> Values => parsers.Values;
    public int Count => parsers.Count;

    /// <inheritdoc />
    public IParserPoolBuilder AddParser<TParsed, [DynamicallyAccessedMembers( DynamicallyAccessedMemberTypes.PublicConstructors )] TParser>( )
        where TParsed : class, IParsed<TParsed>
        where TParser : class, IParser<TParsed>
    {
        parsers[ typeof( TParsed ) ] = typeof( TParser );
        return this;
    }

    public bool ContainsKey( Type key ) => parsers.ContainsKey( key );
    public IEnumerator<KeyValuePair<Type, Type>> GetEnumerator( ) => parsers.GetEnumerator();
    public bool TryGetValue( Type key, [MaybeNullWhen( false )] out Type value ) => parsers.TryGetValue( key, out value );
    IEnumerator IEnumerable.GetEnumerator( ) => GetEnumerator();
}