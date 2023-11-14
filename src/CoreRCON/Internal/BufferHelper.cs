using System.Runtime.InteropServices;
using System.Text;

namespace CoreRCON.Internal;

internal static class BufferHelper
{
#if NETSTANDARD2_1_OR_GREATER
    [Obsolete("Prefer usage of Memory types.")]
#endif
    public static ArraySegment<byte> AsSegment(this ReadOnlyMemory<byte> buffer)
    {
        if (!MemoryMarshal.TryGetArray(buffer, out var segment))
        {
            segment = new(buffer.ToArray());
        }

        return segment;
    }

    /// <summary> Step through a byte array and read a null-terminated string. </summary>
    /// <param name="bytes">Byte array.</param>
    /// <param name="start">Offset to start reading from.</param>
    /// <param name="i">Offset variable to move to the end of the string.</param>
    /// <returns>UTF-8 encoded string.</returns>
    public static string ReadNullTerminatedString(this byte[] bytes, int start, ref int i)
    {
        var end = Array.IndexOf(bytes, (byte)0, start);
        if (end < 0) throw new ArgumentOutOfRangeException(nameof(bytes), "Byte array does not appear to contain a null byte to stop reading a string at.");
        i = end + 1;
        return Encoding.UTF8.GetString(bytes, start, end - start);
    }

    public static List<string> ReadNullTerminatedStringArray(this byte[] bytes, int start, ref int i)
    {
        var result = new List<string>();
        var byteindex = start;
        while (bytes[ byteindex ] != 0x00)
        {
            result.Add(bytes.ReadNullTerminatedString(byteindex, ref byteindex));
        }
        i = byteindex + 1;
        return result;
    }

    public static Dictionary<string, string> ReadNullTerminatedStringDictionary(this byte[] bytes, int start, ref int i)
    {
        var result = new Dictionary<string, string>();
        var byteindex = start;
        while (bytes[ byteindex ] != 0x00)
        {
            result.Add(bytes.ReadNullTerminatedString(byteindex, ref byteindex), bytes.ReadNullTerminatedString(byteindex, ref byteindex));
        }
        i = byteindex + 1;
        return result;
    }

    /// <summary>
    /// Read a short from a byte array and update the offset.
    /// </summary>
    /// <param name="bytes">Byte array.</param>
    /// <param name="start">Offset to start reading from.</param>
    /// <param name="i">Offset variable to move to the end of the string.</param>
    public static short ReadShort(this byte[] bytes, int start, ref int i)
    {
        i += 2;
        return BitConverter.ToInt16(bytes, start);
    }

    /// <summary>
    /// Read a float from a byte array and update the offset.
    /// </summary>
    /// <param name="bytes">Byte array.</param>
    /// <param name="start">Offset to start reading from.</param>
    /// <param name="i">Offset variable to move to the end of the string.</param>
    public static float ReadFloat(this byte[] bytes, int start, ref int i)
    {
        i += 4;
        return BitConverter.ToSingle(bytes, start);
    }

    public static bool TryGetString(byte[] buffer, int offset, int length, out string value)
    {
#if NETSTANDARD2_1_OR_GREATER
        ReadOnlySpan<byte> casted = buffer;
        return TryGetString(
            casted.Slice(offset, length),
            out value);
#else
        try
        {
            value = Encoding.UTF8.GetString(buffer, offset, Math.Min(length, buffer.Length));
            return true;
        }
        catch (ArgumentException)
        {
            value = string.Empty;
            return false;
        }
#endif
    }

#if NETSTANDARD2_1_OR_GREATER
    public static bool TryGetString(ReadOnlySpan<byte> buffer, out string value)
    {
        try
        {
            value = Encoding.UTF8.GetString(buffer);
            return true;
        }
        catch (ArgumentException)
        {
            value = string.Empty;
            return false;
        }
    }
#endif
}
