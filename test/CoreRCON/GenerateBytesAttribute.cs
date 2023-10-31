using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit.Sdk;

namespace CoreRCON.Tests;

internal sealed class GenerateBytesAttribute(int count, int length = 256) : DataAttribute
{
    public int Count { get; } = count;
    public int MaxLength { get; set; } = length;
    public bool RandomizeLength { get; set; }

    public override IEnumerable<object[]> GetData(MethodInfo methodInfo)
    {
        using var rng = RandomNumberGenerator.Create();
        for (var i = 0; i < Count; i++)
        {
            var data = new byte[RandomizeLength ? Random.Shared.Next(MaxLength) : MaxLength];
            rng.GetBytes(data);

            yield return [data];
        }
    }

    /* public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
    {
        var builder = new StringBuilder(methodInfo.Name).Append(" (");

        var bytes = (byte[])data![0]!;
        return builder.AppendJoin(", ", bytes).Append(')').ToString();
    } */
}
