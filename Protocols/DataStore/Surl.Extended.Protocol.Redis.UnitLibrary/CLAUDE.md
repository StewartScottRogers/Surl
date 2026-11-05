# Surl.Extended.Protocol.Redis.UnitLibrary

An extended server (ADR-0083): it will answer the `redis` and `rediss` schemes the Curl port adds,
which upstream curl does not have. ADR-0106 decides how. It is judged by its specifications - the
RESP page, RESP3.md and each command's reference page - never by the Curl port's output, and its
unit tests replay bytes written by hand from those pages' examples.

What is here so far is the RESP codec and the command reader (BL-488, ADR-0106 decisions 2, 5
and 6):

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
  past `--max-filesize` plus 64 KiB, closed with no bytes; or `ConnectionClosed`.

The server itself and the connection commands (BL-489), the keyspace and string commands
(BL-490), hashes, lists, sets and pub/sub (BL-491) and the command line (BL-492) build on it.

This library references `Surl.Protocol.Abstractions.UnitLibrary` only (BL-678 adds
`Surl.MessageBus`) - never another protocol server, original or extended. Never construct a
`Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here; the transport comes from
the listener seam.
