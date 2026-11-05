# Surl.Extended.Protocol.Redis.UnitLibrary

An extended server (ADR-0083): it will answer the `redis` and `rediss` schemes the Curl port adds,
which upstream curl does not have. ADR-0106 decides how. It is judged by its specifications - the
RESP page, RESP3.md and each command's reference page - never by the Curl port's output, and its
unit tests replay bytes written by hand from those pages' examples.

What is here so far is the RESP codec, the command reader (BL-488, ADR-0106 decisions 2, 5
and 6), and the server with its connection commands (BL-489, decisions 1, 2, 4, 5 and 6):

- `RespValue` is one reply; each subclass is one RESP2 or RESP3 type and `Encode()` writes it:
  `RespSimpleString` (`+`), `RespSimpleError` (`-`), `RespInteger` (`:`), `RespBulkString` (`$`),
  `RespNullBulkString` (`$-1`), `RespArray` (`*`), `RespNullArray` (`*-1`), `RespNull` (`_`),
  `RespBoolean` (`#`), `RespDouble` (`,`, with `inf`, `-inf` and `nan`), `RespBigNumber` (`(`),
  `RespBulkError` (`!`), `RespVerbatimString` (`=`), `RespMap` (`%`), `RespAttribute` (`|`, then
  the reply it describes), `RespSet` (`~`) and `RespPush` (`>`). A value is written as built:
  picking the RESP2 or RESP3 form of a reply is the caller's, by the connection's version.
- `RedisCommandReader.ReadCommandAsync` reads a connection's commands one at a time, however
  their bytes are split: arrays of bulk strings, and inline commands split by
  `RedisInlineCommandSplitter` with Redis's quoting. It skips blank lines, `*0` and negative
  counts, and returns a `RedisCommandReadOutcome`: the command's arguments; a protocol error with
  its `-ERR Protocol error: …` reply (invalid bulk or multibulk length, `expected '$'`, too big
  inline request, unbalanced quotes) for the server to write before closing; `CommandTooLarge`
  past `--max-filesize` plus 64 KiB, closed with no bytes; or `ConnectionClosed`. With
  `AppliesUnauthenticatedLimits` set it refuses more than 10 elements or a bulk string over
  16384 bytes, as Redis 7 does before a login; `WaitForCommandStartAsync` waits for a command's
  first byte so the server can time the command from it.
- `RedisServer` is the `IConnectionProtocolServer` (`redis`, `rediss`) and the
  `IConnectionRefusalWriter` (`-ERR max number of clients reached`). `RedisServerConnection`
  runs one connection: it asks the policy once whether an anonymous caller is let in, applies
  `--head-timeout` to the first command from accept and to each later one from its first byte,
  writes a reader protocol error then closes, and closes with no bytes on `CommandTooLarge`.
- `RedisCommandTable` finds a command case-insensitively and answers an unknown command or
  subcommand and a wrong argument count before the login check, as Redis does; `RedisCommand`
  (and `RedisCommandContainer` for `CLIENT`) carries Redis's arity and whether it runs before a
  login. `RedisConnectionCommands` answers `HELLO`, `AUTH`, `SELECT`, `PING`, `ECHO`, `QUIT`,
  `RESET` and `CLIENT SETNAME|GETNAME|ID|SETINFO`; `RedisSession` is one connection's state
  (client id, RESP version, login, database, name); `RedisLogins` asks the policy;
  `RedisErrorReplies` holds ADR-0106 decision 5's texts; `RedisIntegers` reads integers as
  Redis does.

The keyspace and string commands (BL-490) and hashes, lists, sets and pub/sub (BL-491) add
their commands to `RedisCommandTable`; the command line (BL-492) serves the server.

This library references `Surl.Protocol.Abstractions.UnitLibrary` only (BL-678 adds
`Surl.MessageBus`) - never another protocol server, original or extended. Never construct a
`Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here; the transport comes from
the listener seam.
