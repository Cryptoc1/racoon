# Racoon.Tool

[![Version](https://img.shields.io/nuget/vpre/Racoon.Tool)](https://www.nuget.org/packages/Racoon.Tool)

A .NET tool for connecting to a RCON shell.

## Getting Started

```bash
$ dotnet tool install -g Racoon.Tool
```
```bash
$ racoon -h
USAGE:
    racoon <host> [password] [OPTIONS]

ARGUMENTS:
    <host>        The IP address or hostname of the server to connect to
    [password]    The RCON password for the server. If not provided, you will be prompted

OPTIONS:
                          DEFAULT
    -h, --help                        Prints help information
        --no-logo                     Suppress the logo display (the $RACOON_NOLOGO environment variable is also supported)
    -p, --port            27015       The remote port to connect to
    -t, --timeout         00:00:30    The timeout duration to use when connecting and sending commands
        --use-koraktor                Whether to use the Koraktor Method for reading packets
```
