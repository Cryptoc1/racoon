using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace CoreRCON.Parsers.Internal;

internal static class IPEndPointParser
{
    public static bool TryParse(string? value, [NotNullWhen(true)] out IPEndPoint endpoint)
    {
#if NETSTANDARD2_1_OR_GREATER
        var segments = value?.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (segments?.Length is 2 && IPAddress.TryParse(segments[ 0 ].Trim(), out var address) && int.TryParse(segments[ 1 ].Trim(), out var port))
        {
            endpoint = new(address, port);
            return true;
        }

        endpoint = default!;
        return false;
#else
        var segments = value?.Split([ ':' ], StringSplitOptions.RemoveEmptyEntries);
        if (segments?.Length is 2 && IPAddress.TryParse(segments[ 0 ].Trim(), out var address) && int.TryParse(segments[ 1 ].Trim(), out var port))
        {
            endpoint = new(address, port);
            return true;
        }

        endpoint = default!;
        return false;
#endif
    }
}
