namespace CoreRCON;

public enum RCONConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Authenticated
}

// SERVERDATA_AUTH_RESPONSE and SERVERDATA_EXECCOMMAND are both 2
public enum RCONPacketType
{
    // SERVERDATA_RESPONSE_VALUE
    Response = 0,

    // SERVERDATA_AUTH_RESPONSE
    AuthResponse = 2,

    // SERVERDATA_EXECCOMMAND
    ExecCommand = 2,

    // SERVERDATA_AUTH
    Auth = 3
}
