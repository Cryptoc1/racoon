using System.Reflection;
using System.Security.Cryptography;
using Xunit.Sdk;

namespace Racoon.Tests;

internal sealed class GenerateBytesAttribute( int count, int length = 256 ) : DataAttribute
{
    private static readonly Random Rng = new();

    public int Count { get; } = count;
    public int MaxLength { get; set; } = length;
    public bool RandomizeLength { get; set; }

    public override IEnumerable<object[]> GetData( MethodInfo methodInfo )
    {
        using var rng = RandomNumberGenerator.Create();
        for( var i = 0; i < Count; i++ )
        {
            var data = new byte[ RandomizeLength ? Rng.Next( MaxLength ) : MaxLength ];
            rng.GetBytes( data );

            yield return [ data ];
        }
    }
}
