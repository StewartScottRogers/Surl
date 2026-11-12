# ADR-0106 — How the Redis server answers `redis` and `rediss`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-487. BL-488 builds the RESP2 and RESP3 codec, the command reader and the protocol errors
  (decisions 2, 5 and 6), BL-489 the connection commands, `HELLO`, `AUTH` and `SELECT` (decisions 1,
  2, 4 and 5), BL-490 the keyspace, the string commands and expiry (decisions 1 and 3), BL-491 the
  hash, list and set commands and pub/sub inside the server (decision 1), BL-678 moves pub/sub onto
  the shared bus (ADR-0100 decision 3.8), and BL-492 serves `redis` and `rediss` from `surl`
  (decision 7).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof),
  [ADR-0095](ADR-0095-the-protocols-datastore-category-folder.md) (the `Protocols/DataStore/` folder),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decisions 3.8 (Redis pub/sub joins the bus) and 4.1 (Redis logs in through `IAuthenticationPolicy`,
  the name `default` when none is given), [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decision 3.4 (`-NOPERM` for a denied channel), [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  (secure by default) and [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits and fixed
  error text).
- **Amends:** nothing.

## Context

`Surl.Extended.Protocol.Redis` (in `Protocols/DataStore/`, ADR-0083's table and ADR-0095) answers the
`redis` and `rediss` schemes the Curl port adds, both on port 6379 (ADR-0083 decision 7). By ADR-0083
decision 1 it is judged by its specification, not by upstream curl, which has no Redis, and not by
the Curl port. The specifications read (2026-11-05), and pinned, are:

- **the Redis serialization protocol specification**
  (<https://redis.io/docs/latest/develop/reference/protocol-spec/>; below "the RESP page"): RESP2 and
  RESP3 types, "Sending commands to a Redis server", inline commands, pipelining, "Protocol
  versions" and `HELLO`;
- **the RESP3 specification** (redis/redis-specifications, `protocol/RESP3.md`): every RESP3 type,
  push data and attributes;
- **the command reference page of each command served** (<https://redis.io/docs/latest/commands/>,
  one page a command, e.g. `.../commands/hello/`), including each page's "RESP2 Reply" and "RESP3
  Reply" sections, and the "Pub/Sub" page (<https://redis.io/docs/latest/develop/interact/pubsub/>);
- **the "Redis persistence" page** (<https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/>),
  which documents running with persistence disabled;
- **the IANA provisional `redis` and `rediss` URI scheme registrations**
  (<https://www.iana.org/assignments/uri-schemes/prov/redis>, `.../prov/rediss>`).

The port's `Curl.Extended.Protocol.Redis.UnitLibrary` and its matrix `Redis.md` were read only to
learn what a client sends, so the server accepts it: an array of bulk strings per command; `HELLO 3`
first, with `AUTH <user> <password>` when credentials are given (`default` when only a password is),
falling back to RESP2 and `AUTH` when `HELLO` is refused with anything but `-NOAUTH` or
`-WRONGPASS`; `SELECT <db>` when the URL path names one; then one command per transfer, from the
URL path's segments and `-d`. Nothing below is taken from the port's output, and no expected byte
comes from it.

Where the command pages leave a server a choice, this ADR decides for the simplest server that a
Redis client cannot tell from Redis Open Source 7.4 on the commands served.

## Decision

### 0. What the server is

- **A standalone Redis server with in-memory databases.** `HELLO` reports `server` `redis`, `version`
  `7.4.0` (the command semantics followed, so a client library's feature detection takes the 7.x
  paths; it is not surl's version, ADR-0006 section 3), `mode` `standalone`, `role` `master` and an
  empty `modules` array. There is no cluster, no replication, no scripting, no transactions
  (`MULTI`/`EXEC`), no modules and no `CONFIG`.
- **The library** references `Surl.Protocol.Abstractions` only (BL-488); BL-678 adds
  `Surl.MessageBus`. It never references another protocol server.
- **Commands on a connection are read and answered one at a time, in order** (the RESP page,
  "Pipelining"): a client may send many commands before reading; each reply is written in the order
  of its command. Client ids are 64-bit integers from 1, one per connection, in accept order, fixed
  for the server's life.

### 1. The command set (each command's reference page)

Command names are case-insensitive (the RESP page). The commands served:

| Family | Commands |
| --- | --- |
| Connection | `HELLO`, `AUTH`, `SELECT`, `PING`, `ECHO`, `QUIT`, `RESET`, `CLIENT SETNAME`, `CLIENT GETNAME`, `CLIENT ID`, `CLIENT SETINFO` |
| Keys | `DEL`, `UNLINK`, `EXISTS`, `EXPIRE`, `PEXPIRE` (with `NX`, `XX`, `GT`, `LT`), `TTL`, `PTTL`, `PERSIST`, `TYPE`, `RENAME`, `KEYS`, `SCAN` (with `MATCH`, `COUNT`, `TYPE`), `DBSIZE`, `FLUSHDB`, `FLUSHALL` (each with `SYNC`/`ASYNC`, alike) |
| Strings | `GET`, `SET` (with `EX`, `PX`, `EXAT`, `PXAT`, `KEEPTTL`, `NX`, `XX`, `GET`), `GETDEL`, `MGET`, `MSET`, `INCR`, `DECR`, `INCRBY`, `DECRBY`, `APPEND`, `STRLEN` |
| Hashes | `HSET`, `HGET`, `HMGET`, `HDEL`, `HEXISTS`, `HLEN`, `HKEYS`, `HVALS`, `HGETALL`, `HINCRBY`, `HSETNX` |
| Lists | `LPUSH`, `RPUSH`, `LPOP`, `RPOP` (each with a count), `LLEN`, `LRANGE`, `LINDEX`, `LSET`, `LREM`, `LTRIM` |
| Sets | `SADD`, `SREM`, `SMEMBERS`, `SISMEMBER`, `SMISMEMBER`, `SCARD`, `SPOP` (with a count), `SINTER`, `SUNION`, `SDIFF` |
| Pub/sub | `SUBSCRIBE`, `UNSUBSCRIBE`, `PSUBSCRIBE`, `PUNSUBSCRIBE`, `PUBLISH` |

- **Every other command** is answered with decision 5's unknown-command error; the connection stays
  open. `CLIENT` with another subcommand is
  `-ERR unknown subcommand '<sub>'. Try CLIENT HELP.`
- **Arity.** A command with the wrong number of arguments for its page's syntax is
  `-ERR wrong number of arguments for '<command>' command` (`<command>` in lower case); an option the page does not list, or options that conflict (`SET` with `NX` and `XX`, or
  two expiry options), is `-ERR syntax error`.
- **Types.** A key holding another type than the command's is
  `-WRONGTYPE Operation against a key holding the wrong kind of value` (each command page). A
  collection emptied by a command (`HDEL`, `LPOP`, `LREM`, `LTRIM`, `SREM`, `SPOP`) removes its key,
  as the pages say.
- **Integers.** An argument that must be an integer and is not, or overflows a signed 64-bit
  integer, is `-ERR value is not an integer or out of range`; `INCR`, `INCRBY`, `DECR`, `DECRBY` and
  `HINCRBY` whose result would overflow are `-ERR increment or decrement would overflow`, the value
  unchanged.
- **Expiry** runs on an injected `TimeProvider`: a key past its expiry is absent to every command
  from that instant (checked on access, so nothing sweeps in the background). `EXPIRE` and `PEXPIRE`
  with a time at or below zero delete the key and answer 1. `SET` with `EX`/`PX` of zero or less, or
  `EXAT`/`PXAT` of zero or less, is `-ERR invalid expire time in 'set' command`. `TTL` rounds the
  remaining milliseconds to the nearest second; `TTL`/`PTTL` answer `-2` for a missing key and `-1`
  for a key without expiry. `SET` without `KEEPTTL` clears an expiry; `RENAME` carries it.
- **Glob patterns** (`KEYS`, `SCAN MATCH`, `PSUBSCRIBE`) follow the `KEYS` page: `*`, `?`, `[abc]`,
  `[^a]`, `[a-z]`, and `\` escaping the next byte, matched on bytes.
- **`SCAN`'s cursor.** Each key gets a sequence number from 1 when it is created in its database.
  `SCAN <cursor>` returns up to `COUNT` (default 10) keys whose number is at or above the cursor, in
  number order, filtered by `MATCH` and `TYPE` after the count is taken (as the `SCAN` page allows),
  and the next cursor (the last number returned plus one), or `0` once no key is left. So, as the
  page guarantees, every key present for a whole iteration is returned, and a key recreated during
  one may be returned twice. A cursor that is not an unsigned integer is `-ERR invalid cursor`.
- **`KEYS`, `SMEMBERS`, `HGETALL` and the like** answer in insertion order, and `SPOP` takes the
  member added first rather than a random one (BL-491); the pages promise no
  order, and a fixed one makes tests pin bytes.
- **Pub/sub** (the "Pub/Sub" page): channels and patterns are byte strings. `SUBSCRIBE` and
  `PSUBSCRIBE` answer one confirmation per channel or pattern
  (`subscribe`/`psubscribe`, the name, the connection's subscription count); `UNSUBSCRIBE` and
  `PUNSUBSCRIBE` with no argument leave every channel or pattern, answering one confirmation each,
  or one with a null name and count 0 when there was none. A message is delivered as `message`,
  channel, payload, or `pmessage`, pattern, channel, payload; a connection matching by a channel and
  a pattern receives both. `PUBLISH` answers the number of subscriptions it was delivered to. Until
  BL-678, delivery is among this server's connections; after it, through the bus as ADR-0100
  decision 3.8 says, with ADR-0102 decision 3.4's `-NOPERM No permissions to access a channel` for
  a denied channel. **In RESP2**, a subscribed connection accepts only `SUBSCRIBE`, `UNSUBSCRIBE`,
  `PSUBSCRIBE`, `PUNSUBSCRIBE`, `PING`, `QUIT` and `RESET`; any other command is
  `-ERR Can't execute '<command>': only (P|S)SUBSCRIBE / (P|S)UNSUBSCRIBE / PING / QUIT / RESET are allowed in this context`,
  and `PING` answers the array `pong` and the message (empty when none). **In RESP3** every command
  stays allowed (the RESP page, push data).
- **Not served**: `SSUBSCRIBE`/`SPUBLISH` (sharded pub/sub is a cluster feature), the blocking list
  commands, `MULTI`/`EXEC`/`WATCH`, `EVAL`, `INFO`, `COMMAND`, `CONFIG`, `OBJECT`, sorted sets,
  streams, bitmaps and HyperLogLog. Each is an unknown command, which every client handles; a later
  task that needs one adds it to this table.

### 2. RESP2, RESP3 and the reply types (the RESP page; RESP3.md; `HELLO`)

- **Every connection starts in RESP2.** `HELLO 3` switches it to RESP3 and `HELLO 2` back, from the
  `HELLO` reply on; `HELLO` with no version keeps the version and answers the same fields. Any other
  version, or one that is not an integer, is `-NOPROTO unsupported protocol version`, and the
  connection keeps its version. `RESET` returns it to RESP2.
- **The `HELLO` reply** is a map in RESP3 and a flat array of the same 14 elements in RESP2:
  `server`, `version`, `proto` (the version now in force), `id` (the client id), `mode`, `role`,
  `modules`, as decision 0 gives them.
- **Reply types by version.** A command's reply is written as its page's "RESP2 Reply" and "RESP3
  Reply" sections say; the ones the served set needs:

  | Reply | RESP2 | RESP3 |
  | --- | --- | --- |
  | a missing value (`GET`, `HGET`, `LPOP` without a count, `CLIENT GETNAME`, `SET … GET` on no value, a failed `SET … NX`/`XX`) | null bulk string `$-1` | null `_` |
  | `LPOP`/`RPOP`/`SPOP` with a count on a missing key | null array `*-1` | null `_` |
  | a missing element inside an array (`MGET`, `HMGET`) | `$-1` | `_` |
  | `HGETALL` | flat array, field then value | map `%` |
  | `SMEMBERS`, `SINTER`, `SUNION`, `SDIFF`, `SPOP` with a count | array | set `~` |
  | `HELLO` | flat array | map `%` |
  | pub/sub confirmations and messages | array `*` | push `>` |
  | 0/1 answers (`EXISTS`, `EXPIRE`, `SISMEMBER`, `HEXISTS`, `HSETNX`, …) | integer | integer (the pages keep integers) |
  | `OK`, `PONG`, `TYPE`'s answer, `RESET` | simple string | simple string |

  Errors are simple errors (`-`) in both versions; no reply uses a double, big number, bulk error,
  verbatim string or attribute, which the codec (BL-488) still writes and reads for its tests.
- **Reading commands** (the RESP page, "Sending commands"): a command is an array of bulk strings,
  read however its bytes are split across reads. A line not starting with `*` is an **inline
  command**: split on spaces and tabs, with `"…"` (backslash escapes `\n`, `\r`, `\t`, `\b`, `\a`,
  `\\`, `\"`, `\xHH`) and `'…'` (only `\'`) quoting as Redis's inline parser; an empty line is
  ignored. `*0` and a negative multibulk length are ignored, as Redis does.

### 3. Databases and persistence

- **16 databases, 0 to 15** (Redis's default `databases 16`). `SELECT <n>` outside them is
  `-ERR DB index is out of range`; a non-integer `-ERR value is not an integer or out of range`.
  `FLUSHDB` empties the selected database, `FLUSHALL` all 16. `DBSIZE` counts the selected one.
  The databases are one set per server, shared by every connection and by `redis` and `rediss`.
- **In memory only.** Nothing is written to disk, with or without `--directory`, and a restart
  starts empty: the persistence page documents persistence disabled as a supported mode, and a test
  server is wanted clean on each run. Redis keys are not files, mail or bus topics (ADR-0100 decision
  3.8), so no shared store holds them. Because nothing reaches the content store, writes need no
  `--allow-uploads` (ADR-0006 section 2 gates the content store); a login is the gate (decision 4).

### 4. Authentication (`AUTH`, `HELLO … AUTH`; ADR-0032; ADR-0100 decision 4.1)

- **Secure by default.** Unless `--allow-anonymous` is given, a connection must log in before any
  command but `AUTH`, `HELLO`, `QUIT` and `RESET`. Another command first is
  `-NOAUTH Authentication required.`; `HELLO` without `AUTH` on such a connection is
  `-NOAUTH HELLO must be called with the client already authenticated, otherwise the HELLO <proto> AUTH <user> <pass> option can be used to authenticate the client and select the RESP protocol version at the same time`.
  The connection stays open.
- **`AUTH <password>`** logs in as the account `default`; **`AUTH <user> <password>`** as `<user>`;
  **`HELLO <version> AUTH <user> <password>`** logs in before the version switch, and **`SETNAME
  <name>`** in `HELLO` names the connection as `CLIENT SETNAME`. Each is judged by
  `IAuthenticationPolicy.CheckPasswordLoginAsync` with the connection's encryption. `Accepted` and
  `AcceptedUnchecked` answer `+OK` (or the `HELLO` reply). `RefusedCredentials`, `RefusedAnonymous`
  and `RefusedPlaintext` all answer, after the policy's refusal delay (ADR-0032 section 8),
  `-WRONGPASS invalid username-password pair or user is disabled.`, naming neither account nor cause
  (ADR-0032 section 5); the verbose log notes which. A refused `HELLO … AUTH` leaves the version
  unchanged. A failed login leaves an earlier login in force, as Redis does.
- **Plain text.** The policy refuses a login on `redis://` without TLS unless
  `--allow-plaintext-auth` is given (FR-028), as for every server; `rediss://` logs in by default.
- **Under `--allow-anonymous`** every command is served without a login, and `AUTH` or `HELLO … AUTH`
  still runs and is accepted unchecked, as ADR-0049 decides for the mail servers. The logged-in
  account, or none, is the caller ADR-0102 judges.
- **`RESET`** logs the connection out (back to the state before `AUTH`), unsubscribes it, selects
  database 0, clears its name, returns it to RESP2 and answers `+RESET`.
- **`--head-timeout`** bounds the time from accept to the first complete command (decision 6).

### 5. Error replies (fixed text, ADR-0006 section 3)

Every error is a simple error whose first word is its code, from this table (each command page's
text); none names a path, an account, an exception or a version:

| Code | Text after the code | When |
| --- | --- | --- |
| `ERR` | `unknown command '<name>', with args beginning with: ` then `'<arg>' ` per argument | a command not served (decision 1) |
| `ERR` | `wrong number of arguments for '<command>' command` | arity |
| `ERR` | `syntax error` | an unknown or conflicting option |
| `ERR` | `value is not an integer or out of range` | an integer argument |
| `ERR` | `increment or decrement would overflow` | `INCR` family overflow |
| `ERR` | `hash value is not an integer` | `HINCRBY` on a field whose value is not an integer (added by BL-491, 2026-11-05) |
| `ERR` | `invalid expire time in '<command>' command` | `SET`, `EXPIRE` family |
| `ERR` | `no such key` | `RENAME`, `LSET` of a missing key |
| `ERR` | `index out of range` | `LSET` |
| `ERR` | `value is out of range, must be positive` | a negative count to `LPOP`, `RPOP`, `SPOP` (added by BL-491, 2026-11-05) |
| `ERR` | `invalid cursor` | `SCAN` |
| `ERR` | `DB index is out of range` | `SELECT` |
| `ERR` | `Client names cannot contain spaces, newlines or special characters.` | `CLIENT SETNAME`, `HELLO … SETNAME` |
| `ERR` | `unknown subcommand '<sub>'. Try CLIENT HELP.` | `CLIENT` |
| `ERR` | `Can't execute '<command>': only (P\|S)SUBSCRIBE / (P\|S)UNSUBSCRIBE / PING / QUIT / RESET are allowed in this context` | RESP2 subscribed (decision 1) |
| `ERR` | `max number of clients reached` | decision 6 |
| `ERR` | `max number of subscriptions reached` | decision 6 (this ADR's own text; Redis has no such bound) |
| `ERR` | `Protocol error: <detail>` | decision 6's framing errors, then close |
| `WRONGTYPE` | `Operation against a key holding the wrong kind of value` | decision 1 |
| `NOAUTH` | `Authentication required.` (or `HELLO`'s text, decision 4) | decision 4 |
| `WRONGPASS` | `invalid username-password pair or user is disabled.` | decision 4 |
| `NOPROTO` | `unsupported protocol version` | decision 2 |
| `NOPERM` | `No permissions to access a channel` | ADR-0102 decision 3.4 (BL-678) |
| `OOM` | `command not allowed when used memory > 'maxmemory'.` | decision 6's store bounds |

- **Echoed text.** A simple error cannot hold CR or LF (the RESP page), so in an echoed name or
  argument each CR and LF becomes a space; the command name is cut to 128 bytes, and the arguments
  part stops once 128 bytes of argument text are written, as Redis's message does. Only the
  client's own bytes are echoed (ADR-0006 section 3 allows what the peer sent).
- `<command>` is the command's name in lower case.

### 6. Limits (ADR-0006)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| a bulk string's length | `--max-filesize` (100 MiB; Redis's `proto-max-bulk-len` is 512 MB); a negative or non-numeric length too | `-ERR Protocol error: invalid bulk length`, then close |
| a multibulk's element count | 1048576 (Redis's bound before 7.0); a non-numeric count too | `-ERR Protocol error: invalid multibulk length`, then close |
| before login (decision 4) | at most 10 elements and 16384 bytes a bulk string, as Redis 7 limits unauthenticated clients | `-ERR Protocol error: unauthenticated multibulk length` / `unauthenticated bulk length`, then close |
| an array element that is not a bulk string | — | `-ERR Protocol error: expected '$', got '<byte>'`, then close |
| a missing CR LF after a bulk string | — | `-ERR Protocol error: invalid bulk length`, then close |
| an inline command | `--max-line` (8192 bytes, CR LF included; Redis's is 64 KiB) | `-ERR Protocol error: too big inline request`, then close |
| unbalanced quotes in an inline command | — | `-ERR Protocol error: unbalanced quotes in request`, then close |
| one command's bytes together | `--max-filesize` plus 64 KiB of framing (Redis's `client-query-buffer-limit`) | close with no bytes |
| the keyspace | 1000000 keys and 256 MiB of key, field, member and value bytes across the 16 databases (Redis's `maxmemory` with `noeviction`) | a command that would grow it is `-OOM command not allowed when used memory > 'maxmemory'.`; reads and deletes still run |
| subscriptions | 10000 channels and patterns a connection | the `SUBSCRIBE`/`PSUBSCRIBE` that passes it is `-ERR max number of subscriptions reached`, nothing subscribed |
| a subscriber's unsent messages | 32 MiB (Redis's `client-output-buffer-limit pubsub` hard limit) | the subscriber's connection is closed with no more bytes; `PUBLISH` counts it as delivered |
| `--head-timeout` (30 s) | from accept, the TLS handshake included, to the first complete command; between commands, from the first byte of the next to its end | close with no bytes |
| `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.Core`'s, subscribers included (a quiet subscriber idles out; the operator raises the option) | close with no bytes |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s, through the server's refusal writer | `-ERR max number of clients reached`, then close (Redis's own answer, ADR-0006 section 5's "the protocol's own error") |

- Every "then close" writes the error and closes after it, as Redis closes a client after a
  protocol error; ADR-0006 section 5's graceful close applies.
- `--max-message` does not apply: RESP has no framed message other than the command, which the
  lines above bound.
- No new option is added: the fixed bounds are a guard, and ADR-0006's options cover what a test
  harness tunes.

### 7. `rediss`

- **`rediss`** is the same server behind implicit TLS from the first byte (the IANA `rediss`
  registration), wrapped in `ImplicitTlsSchemeServer` by BL-492 as `mqtts` is (ADR-0010): TLS 1.2 and
  1.3 (ADR-0006 section 4), `--cert` or `--self-signed` required (`CertificateProblem` (58) without),
  no client certificate asked for, no ALPN required (Redis registers none; one offered is not
  agreed). Its default port is 6379 as `redis`'s (ADR-0083 decision 7), so serving both at once takes
  two listen URLs with two ports.
- The databases and subscriptions are shared by both schemes (decision 3).

## Alternatives considered

- **Persist the databases under `<path>/.surl/redis`.** Rejected: a test server is wanted clean on
  each run, the persistence page documents running without persistence, and a snapshot format
  (Redis's RDB or one of our own) would be work and a file format no client sees. A later task can
  add it behind an option.
- **Start connections in RESP3.** Rejected: the RESP page says every connection starts in RESP2 and
  `HELLO 3` switches it.
- **Serve `INFO` and `COMMAND`.** Rejected for now: their replies are large free-text and metadata
  tables whose content is Redis's, not the protocol's; clients that send them at connect
  (`redis-cli`) handle the unknown-command error.
- **Echo no command name in the unknown-command error.** Rejected: the text is the page's, clients
  and people read it, and only the client's own bytes are echoed.
- **Use `--max-message` for the whole command.** Rejected: a 1 MiB default would refuse a `SET` of a
  2 MiB value that a 100 MiB upload limit allows elsewhere; a bulk string is a value, like an upload.
- **Answer a connection past the limit with no bytes**, as ADR-0006 section 5 does for framed
  protocols without an error. Rejected: Redis has its own error for it, which section 5 prefers.

## Consequences

- `surl redis://127.0.0.1:0/` (with `--allow-anonymous`, or an account) is a standalone Redis
  server with 16 in-memory databases: strings with expiry, hashes, lists, sets and pub/sub, in RESP2
  and RESP3, with `rediss` over TLS.
- BL-488 to BL-491 build it, BL-678 moves pub/sub onto the bus, BL-492 serves it from `surl`, and
  BL-493 runs the Curl port's client against it as a live client (ADR-0083).
- The requirement matrix [`Redis.md`](../../Conformance/Extended/Redis.md) traces each server-side
  requirement to BL-488 to BL-491; each task replaces its ID with the named test.
