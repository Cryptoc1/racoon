namespace Racoon;

/// <summary> Represents an exception that occurs while performing operations against a RCON Host. </summary>
public class RCONException : Exception
{
    /// <inheritdoc/>
    public RCONException( )
    {
    }

    /// <inheritdoc/>
    public RCONException( string message ) : base( message )
    {
    }

    /// <inheritdoc/>
    public RCONException( string message, Exception? inner ) : base( message, inner )
    {
    }
}

/// <summary> Represents an exception that occurs while authenticating with an RCON Host. </summary>
public class RCONAuthenticationException : RCONException
{
    /// <inheritdoc/>
    public RCONAuthenticationException( )
    {
    }

    /// <inheritdoc/>
    public RCONAuthenticationException( string message ) : base( message )
    {
    }

    /// <inheritdoc/>
    public RCONAuthenticationException( string message, Exception? inner ) : base( message, inner )
    {
    }
}

/// <summary> Represents an exception that occurs while sending a command to a RCON host. </summary>
public class RCONCommandException : RCONException
{
    /// <summary> The command text that resulted in the exception to be thrown. </summary>
    public string Command { get; }

    /// <inheritdoc/>
    public RCONCommandException( string message, string command ) : base( message )
    {
        Command = command;
    }

    /// <inheritdoc/>
    public RCONCommandException( string message, string command, Exception? inner ) : base( message, inner )
    {
        Command = command;
    }

    internal static RCONCommandException Failed( string command, Exception? inner = null )
    {
        return new( $"Failed to execute command '{command}'.", command, inner );
    }
}
