using System.Net;

namespace Racoon.Tests;

/// <summary> Helper class that ensures only a single instance of <see cref="RCONServer"/> is used within tests (port locking in concurrent tests) </summary>
internal sealed class RCONServerAccessor( RCONServer server ) : IDisposable
{
    private static readonly SemaphoreSlim Locker = new( 1, 1 );

    public RCONServer Server { get; } = server;

    public static async Task<RCONServerAccessor> Acquire( )
    {
        await Locker.WaitAsync();

        // NOTE: give the OS time to release the socket; prevents tests from hanging
        await Task.Delay( 175 );

        return new( new RCONServer( new( IPAddress.Loopback, 27015 ), "TEST", new()
        {
            MaxConnections = int.MaxValue
        } ) );
    }

    public void Dispose( )
    {
        Server.Dispose();
        Locker.Release();
    }
}
