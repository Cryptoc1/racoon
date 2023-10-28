using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace CoreRCON;

internal static class Extensions
{
    /// <summary> Step through a byte array and read a null-terminated string. </summary>
    /// <param name="bytes">Byte array.</param>
    /// <param name="start">Offset to start reading from.</param>
    /// <param name="i">Offset variable to move to the end of the string.</param>
    /// <returns>UTF-8 encoded string.</returns>
    public static string ReadNullTerminatedString(this byte[] bytes, int start, ref int i)
    {
        int end = Array.IndexOf(bytes, (byte)0, start);
        if (end < 0) throw new ArgumentOutOfRangeException(nameof(bytes), "Byte array does not appear to contain a null byte to stop reading a string at.");
        i = end + 1;
        return Encoding.UTF8.GetString(bytes, start, end - start);
    }

    public static List<string> ReadNullTerminatedStringArray(this byte[] bytes, int start, ref int i)
    {
        var result = new List<string>();
        var byteindex = start;
        while (bytes[byteindex] != 0x00)
        {
            result.Add(ReadNullTerminatedString(bytes, byteindex, ref byteindex));
        }
        i = byteindex + 1;
        return result;
    }

    public static Dictionary<string, string> ReadNullTerminatedStringDictionary(this byte[] bytes, int start, ref int i)
    {
        var result = new Dictionary<string, string>();
        var byteindex = start;
        while (bytes[byteindex] != 0x00)
        {
            result.Add(ReadNullTerminatedString(bytes, byteindex, ref byteindex), ReadNullTerminatedString(bytes, byteindex, ref byteindex));
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

    /// <summary> Receives a block of memory asyncronosly </summary>
    /// <param name="socket">Socket to receive from</param>
    /// <param name="memory">Memory segment to receive to</param>
    /// <param name="socketFlags">Flags for socket</param>
    /// <returns>Awaitable task resolving to the number of bytes received</returns>
    public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags)
    {
        ReadOnlyMemory<byte> casted = memory;
        if (!MemoryMarshal.TryGetArray(casted, out var buffer))
        {
            throw new ArgumentException("Expected an Array-backed buffer.", nameof(memory));
        }

        return SocketTaskExtensions.ReceiveAsync(socket, buffer, socketFlags);
    }

    // See https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/Scenarios/Infrastructure/TaskExtensions.cs
    public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan? timeout)
    {
        timeout ??= TimeSpan.Zero;
        if (timeout == TimeSpan.Zero)
        {
            return await task;
        }

        using var cancellation = new CancellationTokenSource();
        if (await Task.WhenAny(task, Task.Delay(timeout.Value, cancellation.Token)) == task)
        {
            cancellation.Cancel();
            return await task;
        }

        throw new TimeoutException($"An asynchronous operation exceeded the configured timeout of '{timeout.Value}'.");
    }
}
