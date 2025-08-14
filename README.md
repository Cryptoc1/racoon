<h1 align="center">Racoon</h1>

<div align="center">

![Version](https://img.shields.io/github/v/tag/cryptoc1/racoon)
![Language](https://img.shields.io/github/languages/top/cryptoc1/racoon)
[![Checks](https://img.shields.io/github/checks-status/cryptoc1/racoon/main)](https://github.com/cryptoc1/racoon/actions/workflows/default.yml)
[![Coverage](https://img.shields.io/codecov/c/github/cryptoc1/racoon)](https://app.codecov.io/gh/cryptoc1/racoon)

</div>

Racoon, originally a fork of [CoreRCON](https://github.com/Challengermode/CoreRcon), is an implementation of the RCON protocol in pure .NET.

## Features

- [`Racoon`](https://github.com/cryptoc1/racoon/tree/main/src/Racoon): The main library
  - Supports connecting to a RCON server via [`RCONClient`](https://github.com/cryptoc1/racoon/tree/main/src/Racoon/RCONClient.cs)
  - Supports hosting a RCON server via [`RCONServer`](https://github.com/cryptoc1/racoon/tree/main/src/Racoon/RCONServer.cs)
- [`Racoon.Extensions.CounterStrike`](https://github.com/cryptoc1/racoon/tree/main/src/Racoon.Extensions.CounterStrike): Enhanced support for connecting to CS2 RCON servers
  - Provides parsers for common messages in CS2
  - Provides [extensions](https://github.com/cryptoc1/racoon/tree/main/src/Racoon.Extensions.CounterStrike/RCONClientExtensions.cs) for common CS2 console commands
- [`Racoon.Parsers`](https://github.com/cryptoc1/racoon/tree/main/src/Racoon.Parsers): Low-level message parsing library
  - Defines the [`IParser<T>`](https://github.com/cryptoc1/racoon/tree/main/src/Racoon.Parsers/Abstractions/IParser{T}.cs) interface
  - Provides built-in parsers for standard RCON message, such as [`ChatMessage`]((https://github.com/cryptoc1/racoon/tree/main/src/Racoon.Parsers/Standard/ChatMessage.cs))
- [`Racoon.Tool`](https://github.com/cryptoc1/racoon/tree/main/src/Racoon.Tool): Connect to an RCON server in the CLI with a .NET tool:
  ```bash
  $ dotnet tool install -g Racoon.Tool
  $ racoon [host]
  ```

## Credits

This project started as a fork of [CoreRCON](https://github.com/Challengermode/CoreRcon), credit is due to [ScottKaye](https://github.com/ScottKaye) for developing the [original version](https://github.com/ScottKaye/CoreRCON), and the maintainers at [Challengermode](https://www.challengermode.com/) for the version that was forked.
