using System.Text;

namespace Racoon.Internal;

internal static class BufferHelper
{
    public static bool TryGetString( in ReadOnlySpan<byte> buffer, out string value )
    {
        try
        {
            value = Encoding.UTF8.GetString( buffer );
            return true;
        }
        catch( ArgumentException )
        {
            value = string.Empty;
            return false;
        }
    }
}
