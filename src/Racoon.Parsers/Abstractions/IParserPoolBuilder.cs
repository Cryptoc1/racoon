using System.Diagnostics.CodeAnalysis;

namespace Racoon.Parsers.Abstractions;

/// <summary> Describe a type that provides a fluent syntax for constructing a <see cref="ParserPool"/>. </summary>
/// <seealso cref="ParserPool.Create(Action{Racoon.Parsers.Abstractions.IParserPoolBuilder}?)" />
/// <seealso cref="ParserPool.CreateDefault(Action{Racoon.Parsers.Abstractions.IParserPoolBuilder}?)" />
public interface IParserPoolBuilder
{
    /// <summary> Adds a parser to the builder. </summary>
    /// <typeparam name="TParsed"> The parsed typed a parser is being registered to. </typeparam>
    /// <typeparam name="TParser"> The type of parser to register. </typeparam>
    public IParserPoolBuilder AddParser<TParsed, [DynamicallyAccessedMembers( DynamicallyAccessedMemberTypes.PublicConstructors )] TParser>( )
        where TParsed : class, IParsed<TParsed>
        where TParser : class, IParser<TParsed>;
}