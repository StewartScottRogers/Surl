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

The keyspace and the key and string commands (BL-490, decisions 1, 3 and 6):

- `RedisKeyspace` is the server's 16 in-memory databases, one per `RedisServer`, shared by every
  connection and never written to disk. It counts keys and key and value bytes against 1000000
  keys and 256 MiB; `IsFull` makes a growing command `-OOM …`. `SyncRoot` is held for each
  keyspace command, so commands are atomic across connections.
- `RedisDatabase` holds one database's `RedisKeyEntry`s (key, `RedisStoredValue`, expiry in Unix
  milliseconds, `SCAN` sequence number) by name and in creation order. A key is absent from the
  instant `now >= expiry` and removed when next touched; nothing sweeps in the background. Keys
  are Latin-1 strings, one character per byte (`RedisKeys`).
- `RedisStoredValue` is a value of one type (`TypeName`, `ByteCount`); `RedisStringValue` is
  the string type, never changed in place. `RedisCollectionValue` (`RedisHashValue`,
  `RedisListValue`, `RedisSetValue`, BL-491) is changed in place and keeps its own byte count;
  `RedisDatabase.Recount` then recounts the keyspace and removes a key whose collection is empty.
- `RedisKeyspaceCommand.Reading` / `.Growing` wrap a command's answer in the lock and the
  `-OOM` check. `RedisKeyCommands` answers `DEL`, `UNLINK`, `EXISTS`, `EXPIRE`, `PEXPIRE`
  (`RedisExpireConditions`), `TTL`, `PTTL`, `PERSIST`, `TYPE`, `RENAME`, `KEYS`
  (`RedisGlobPattern`), `SCAN` (`RedisScanOptions`), `DBSIZE`, `FLUSHDB` and `FLUSHALL`;
  `RedisStringCommands` answers `GET`, `SET` (`RedisSetOptions`, `RedisSetExpiry`), `GETDEL`,
  `MGET`, `MSET`, `INCR`, `DECR`, `INCRBY`, `DECRBY`, `APPEND` and `STRLEN`. Each command reads
  the clock once: `RedisCommandCall.NowMilliseconds`, from the exchange's injected `TimeProvider`.

Hashes, lists, sets and pub/sub (BL-491, decisions 1, 2 and 6):

- `RedisCollections` reads or changes the collection at a command's key: `-WRONGTYPE …` for
  another type, a new empty collection for a growing command on an absent key, and the recount
  after. `RedisHashCommands`, `RedisListCommands` and `RedisSetCommands` answer the hash, list
  and set commands; fields and members are answered in the order added, and `SPOP` takes the
  member added first (the page's random pick, which no client can tell from a fixed one).
- `RedisPubSub` is the server's channels and patterns, shared by its connections: subscribe
  and unsubscribe confirmations, at most 10000 subscriptions a connection, and `Publish`,
  which reaches channel and matching pattern subscriptions and answers how many. Since BL-678
  (ADR-0100 decision 3.8, ADR-0126) a channel that is a bus topic (`RedisChannelNames`: valid
  UTF-8 and a canonical topic name) is published and subscribed on the shared `MessageBus`, so it
  reaches and hears every messaging protocol; any other channel stays among this server's
  connections. A connection with patterns holds one bus subscription to `#` and sends its
  `pmessage`s from `AfterMessageDelivered`, after the `message`; `PUBLISH` counts each matching
  pattern once; bus refusals are `-NOPERM …`, `-OOM …` and `-ERR max number of subscriptions
  reached`. `RedisServer(policy, messageBus)` takes the process's bus; `RedisServer(policy)`
  makes one of its own. `RedisSubscriber` is one
  connection as pub/sub sees it; `RedisPubSubCommands` answers `SUBSCRIBE`, `UNSUBSCRIBE`,
  `PSUBSCRIBE`, `PUNSUBSCRIBE` and `PUBLISH`, and refuses other commands on a subscribed RESP2
  connection. `RedisReplySequence` writes several confirmations as one reply.
- `RedisReplyWriter` writes all of a connection's bytes, one write at a time: its own replies,
  and messages other connections publish, which the publisher does not wait for. Past 32 MiB
  of unsent messages, or when one cannot be written, it cancels its `Token`, which every read
  and write of the connection uses, so the connection closes with no more bytes.

Faults (BL-985, ADR-0124 decisions 4 and 9):

- `RedisCommandFaults` asks `ExchangeContext.Faults` at `FaultStage.Command` for each command
  about to run - after the unknown-command, argument-count, login and subscribed checks, so a
  fault never masks a refusal. The point names the scheme, listener, account logged in as, the
  command's name upper-cased (`GET`, `CLIENT|SETNAME`) and its first key - or a pub/sub
  command's first channel - as `path`; connection and whole-database commands name none.
  Delay and drop go through `CommandStageFaults`; an `error` rule answers `-<reply>` in place of
  running the command and publishes one `FaultAppliedEvent` (path, else command). A reply that is
  empty or holds CR or LF is noted as not applied. `RedisCommandFaultOutcome` says which.

The command line (BL-492) serves the server.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.MessageBus.UnitLibrary`
(ADR-0100 decision 3.1) - never another protocol server, original or extended. Never construct a
`Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here; the transport comes from
the listener seam.
