# Racoon.Parsers

[![Version](https://img.shields.io/nuget/vpre/Racoon.Parsers)](https://www.nuget.org/packages/Racoon.Parsers)

Low-level message parsing library for Racoon.

## Commonly Used Types
- [`IParser<T>`](https://github.com/cryptoc1/raccon/tree/main/src/Racoon.Parsers/Abstractions/IParser{T}.cs) & [`IParseable<T>`](https://github.com/cryptoc1/raccon/tree/main/src/Racoon.Parsers/Abstractions/IParseable{T}.cs): Entry points for implementing message parsers.
- [`ParserPool`](https://github.com/cryptoc1/raccon/tree/main/src/Racoon.Parsers/ParserPool.cs): Provides resolution+caching of `IParser` instances.
