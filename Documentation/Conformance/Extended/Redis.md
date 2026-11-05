# Redis requirement matrix

The server-side requirements `Surl.Extended.Protocol.Redis` answers, for the `redis` and `rediss`
schemes (ADR-0083 decision 9). Written from the Redis serialization protocol specification
(<https://redis.io/docs/latest/develop/reference/protocol-spec/>), the RESP3 specification
(redis/redis-specifications `protocol/RESP3.md`), the command reference page of each command served,
the "Pub/Sub" page and the IANA provisional `redis` and `rediss` registrations, the specifications
[ADR-0106](../../Planning/Decisions/ADR-0106-how-the-redis-server-answers-redis-and-rediss.md)
decides by, never from the Curl port's client matrix. Each row binds the server; "Covered by" names
the task that proves it, until that task replaces its ID with the named test, or says why the row
needs no test.

The pages state few requirements in RFC keywords, so each reply type a server writes, each rule of
a page and each error a server owes is a row. IDs are `Redis:RESP-<Part>-<n>` for the RESP page and
RESP3.md, `Redis:<COMMAND>-<n>` for a command page, `Redis:PubSub-<n>` for the "Pub/Sub" page,
`Redis:URI-<n>` for the scheme registrations, and `Redis:Surl-<Decision>-<n>` for a rule ADR-0106
adds where the pages leave the server a choice.

## Reading commands (RESP page: "Sending commands to a Redis server", inline commands, pipelining)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Redis:RESP-Request-1 | MUST | A command is read as an array of bulk strings, however its bytes are split across reads. | `RedisCommandReaderTests.ReadCommandAsync_ArrayOfBulkStrings_ReadsEachArgument`, `RedisCommandReaderTests.ReadCommandAsync_CommandSplitAcrossReads_ReadsItWhole` |
| Redis:RESP-Request-2 | MUST | Bulk strings are binary safe: an argument holding NUL or CR LF is read by its length. | `RedisCommandReaderTests.ReadCommandAsync_BulkStringHoldingCrLfAndNul_ReadsItByLength` |
| Redis:RESP-Request-3 | MUST | A line not starting with `*` is an inline command, split on spaces with Redis's quoting; an empty line is ignored. | `RedisCommandReaderTests.ReadCommandAsync_InlineCommand_SplitsItOnSpaces`, `RedisInlineCommandSplitterTests.Split_Line_ReturnsTheArguments`, `RedisCommandReaderTests.ReadCommandAsync_EmptyLinesAndEmptyArrays_AreSkipped` |
| Redis:RESP-Pipeline-1 | MUST | Several commands sent before any reply is read are each answered, in order. | `RedisCommandReaderTests.ReadCommandAsync_ThreePipelinedCommands_ReadsThemOneByOneInOrder` |
| Redis:RESP-Request-4 | MUST | `*0` and a negative multibulk length are ignored. | `RedisCommandReaderTests.ReadCommandAsync_EmptyLinesAndEmptyArrays_AreSkipped` |
| Redis:Surl-Limits-1 | MUST | A bulk length that is negative, not numeric or above `--max-filesize`, or a bulk string without its CR LF, is `-ERR Protocol error: invalid bulk length`, then close. | `RedisCommandReaderTests.ReadCommandAsync_BadBulkLengthOrMissingCrLf_IsInvalidBulkLength` |
| Redis:Surl-Limits-2 | MUST | A multibulk count that is not numeric or above 1048576 is `-ERR Protocol error: invalid multibulk length`, then close. | `RedisCommandReaderTests.ReadCommandAsync_BadMultibulkCount_IsInvalidMultibulkLength` |
| Redis:Surl-Limits-3 | MUST | Before login, more than 10 elements or a bulk string over 16384 bytes is `-ERR Protocol error: unauthenticated multibulk length` / `unauthenticated bulk length`, then close. | BL-489 |
| Redis:Surl-Limits-4 | MUST | An array element not a bulk string is `-ERR Protocol error: expected '$', got '<byte>'`, then close. | `RedisCommandReaderTests.ReadCommandAsync_ElementNotABulkString_IsExpectedDollar` |
| Redis:Surl-Limits-5 | MUST | An inline command over `--max-line` is `-ERR Protocol error: too big inline request`, and unbalanced quotes `-ERR Protocol error: unbalanced quotes in request`; each then close. | `RedisCommandReaderTests.ReadCommandAsync_InlineLineLongerThanMaxLine_IsTooBigInlineRequest`, `RedisCommandReaderTests.ReadCommandAsync_InlineUnbalancedQuotes_IsUnbalancedQuotesInRequest` |
| Redis:Surl-Limits-6 | MUST | One command's bytes past `--max-filesize` plus 64 KiB close the connection with no bytes. | `RedisCommandReaderTests.ReadCommandAsync_CommandPastMaxFilesizeAndFraming_IsCommandTooLarge` (the reader; BL-489's server closes) |
| Redis:Surl-Limits-7 | MUST | No complete command by `--head-timeout` closes the connection with no bytes. | BL-489 |
| Redis:Surl-Limits-8 | MUST | A connection past `--max-connections` or `--max-connections-per-address` is answered `-ERR max number of clients reached`, then closed. | BL-489 |

## Reply types (RESP page; RESP3.md)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Redis:RESP-SimpleString-1 | MUST | `+` simple string. | `RespValueEncodingTests.Encode_SimpleString_IsPlusTextCrLf` |
| Redis:RESP-SimpleError-1 | MUST | `-` simple error, its first word the error code; no CR or LF inside. | `RespValueEncodingTests.Encode_SimpleError_IsMinusTextCrLf`, `RespValueEncodingTests.Constructor_SimpleStringOrErrorWithALineBreak_Throws` |
| Redis:RESP-Integer-1 | MUST | `:` signed 64-bit integer. | `RespValueEncodingTests.Encode_Integer_IsColonDecimalCrLf` |
| Redis:RESP-BulkString-1 | MUST | `$` bulk string by its length. | `RespValueEncodingTests.Encode_BulkString_IsLengthThenBytes`, `RespValueEncodingTests.Encode_BulkStringHoldingCrLf_IsBinarySafe` |
| Redis:RESP-Array-1 | MUST | `*` array, elements of any type, nested. | `RespValueEncodingTests.Encode_Array_IsCountThenEachElement`, `RespValueEncodingTests.Encode_NestedArray_WritesTheInnerArraysInPlace` |
| Redis:RESP2-Null-1 | MUST | `$-1` null bulk string in RESP2. | `RespValueEncodingTests.Encode_NullBulkString_IsDollarMinusOne` |
| Redis:RESP2-Null-2 | MUST | `*-1` null array in RESP2. | `RespValueEncodingTests.Encode_NullArray_IsStarMinusOne` |
| Redis:RESP-Null-1 | MUST | `_` null in RESP3. | `RespValueEncodingTests.Encode_Null_IsUnderscore` |
| Redis:RESP-Boolean-1 | MUST | `#t` / `#f` boolean. | `RespValueEncodingTests.Encode_Boolean_IsHashTOrF` |
| Redis:RESP-Double-1 | MUST | `,` double, `inf`, `-inf` and `nan` included. | `RespValueEncodingTests.Encode_Double_IsCommaShortestDecimalOrInfOrNan` |
| Redis:RESP-BigNumber-1 | MUST | `(` big number. | `RespValueEncodingTests.Encode_BigNumber_IsParenthesisDecimal` |
| Redis:RESP-BulkError-1 | MUST | `!` bulk error. | `RespValueEncodingTests.Encode_BulkError_IsBangLengthThenBytes` |
| Redis:RESP-VerbatimString-1 | MUST | `=` verbatim string: three-byte format, colon, text. | `RespValueEncodingTests.Encode_VerbatimString_IsEqualsLengthFormatColonText` |
| Redis:RESP-Map-1 | MUST | `%` map of key and value pairs. | `RespValueEncodingTests.Encode_Map_IsPercentPairCountThenKeyAndValue` |
| Redis:RESP-Attribute-1 | MUST | `\|` attribute. | `RespValueEncodingTests.Encode_Attribute_IsPipePairsThenTheReply` |
| Redis:RESP-Set-1 | MUST | `~` set. | `RespValueEncodingTests.Encode_Set_IsTildeCountThenEachElement` |
| Redis:RESP-Push-1 | MUST | `>` push data. | `RespValueEncodingTests.Encode_Push_IsGreaterThanCountThenEachElement` |

## Connection commands (`HELLO`, `AUTH`, `SELECT`, `PING`, `ECHO`, `QUIT`, `RESET`, `CLIENT`)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Redis:HELLO-1 | MUST | A connection starts in RESP2; `HELLO 3` switches it to RESP3 and `HELLO 2` back, from the `HELLO` reply on. | BL-489 |
| Redis:HELLO-2 | MUST | `HELLO`'s reply holds `server`, `version`, `proto`, `id`, `mode`, `role` and `modules`: a map in RESP3, a flat array in RESP2. | BL-489 |
| Redis:HELLO-3 | MUST | An unsupported or non-integer version is `-NOPROTO unsupported protocol version`, the version unchanged. | BL-489 |
| Redis:HELLO-4 | MUST | `HELLO <v> AUTH <user> <password>` logs in before the switch; a refusal leaves the version unchanged. | BL-489 |
| Redis:HELLO-5 | MUST | `HELLO <v> SETNAME <name>` names the connection. | BL-489 |
| Redis:HELLO-6 | MUST | `HELLO` without `AUTH` before a required login is `-NOAUTH HELLO must be called with the client already authenticated, …`. | BL-489 |
| Redis:AUTH-1 | MUST | `AUTH <password>` logs in as `default`; `AUTH <user> <password>` as `<user>`; `+OK` when accepted. | BL-489 |
| Redis:AUTH-2 | MUST | A refused login is `-WRONGPASS invalid username-password pair or user is disabled.`, after the refusal delay, an earlier login kept. | BL-489 |
| Redis:Surl-Auth-1 | MUST | Unless `--allow-anonymous`, any command but `AUTH`, `HELLO`, `QUIT` and `RESET` before login is `-NOAUTH Authentication required.`, the connection kept. | BL-489 |
| Redis:Surl-Auth-2 | MUST | Under `--allow-anonymous` every command is served without login, and `AUTH` is accepted unchecked. | BL-489 |
| Redis:Surl-Auth-3 | MUST | A login on `redis://` without TLS is refused unless `--allow-plaintext-auth`, with the `-WRONGPASS` text. | BL-489 |
| Redis:SELECT-1 | MUST | `SELECT 0` to `SELECT 15` pick the database; another index is `-ERR DB index is out of range`, a non-integer `-ERR value is not an integer or out of range`. | BL-489 |
| Redis:PING-1 | MUST | `PING` is `+PONG`, `PING <msg>` the bulk string `<msg>`, in both versions. | BL-489 |
| Redis:ECHO-1 | MUST | `ECHO <msg>` is the bulk string `<msg>`. | BL-489 |
| Redis:QUIT-1 | MUST | `QUIT` is `+OK`, then the connection closes. | BL-489 |
| Redis:RESET-1 | MUST | `RESET` logs out, unsubscribes, selects 0, clears the name, returns to RESP2 and answers `+RESET`. | BL-489 |
| Redis:CLIENT-1 | MUST | `CLIENT SETNAME`, `GETNAME` (null when unnamed), `ID` and `SETINFO` (`+OK`); a name with spaces or newlines is refused with the page's text. | BL-489 |
| Redis:CLIENT-2 | MUST | Another `CLIENT` subcommand is `-ERR unknown subcommand '<sub>'. Try CLIENT HELP.` | BL-489 |
| Redis:Surl-Commands-1 | MUST | A command not served is `-ERR unknown command '<name>', with args beginning with: …`, CR and LF echoed as spaces, cut to 128 bytes; the connection stays open. | BL-489 |
| Redis:Surl-Commands-2 | MUST | A command with the wrong argument count is `-ERR wrong number of arguments for '<command>' command`. | BL-489 |
| Redis:Surl-Commands-3 | MUST | Command names are case-insensitive. | BL-489 |

## Keys, strings and expiry

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Redis:GET-1 | MUST | `GET` of a missing key is `$-1` in RESP2 and `_` in RESP3. | BL-490 |
| Redis:SET-1 | MUST | `SET` answers `+OK`; a failed `NX` or `XX` the null reply; `GET` the old value or null. | BL-490 |
| Redis:SET-2 | MUST | `EX`, `PX`, `EXAT`, `PXAT` and `KEEPTTL` set or keep the expiry; a non-positive time is `-ERR invalid expire time in 'set' command`; conflicting options `-ERR syntax error`. | BL-490 |
| Redis:SET-3 | MUST | `SET k v PX 100` leaves the key absent once the injected clock has advanced 100 ms. | BL-490 |
| Redis:GETDEL-1 | MUST | `GETDEL` answers the value and deletes the key. | BL-490 |
| Redis:MGET-1 | MUST | `MGET` answers an array with a null for each missing or non-string key; `MSET` answers `+OK`. | BL-490 |
| Redis:INCR-1 | MUST | `INCR`, `DECR`, `INCRBY`, `DECRBY` on a missing key start from 0; on a non-integer value `-ERR value is not an integer or out of range`; on overflow `-ERR increment or decrement would overflow`, the value unchanged. | BL-490 |
| Redis:APPEND-1 | MUST | `APPEND` answers the new length; `STRLEN` the length, 0 when missing. | BL-490 |
| Redis:DEL-1 | MUST | `DEL`, `UNLINK` and `EXISTS` count the keys, a key named twice counted twice by `EXISTS`. | BL-490 |
| Redis:EXPIRE-1 | MUST | `EXPIRE`/`PEXPIRE` answer 1 when set and 0 otherwise, honour `NX`, `XX`, `GT`, `LT`, and delete the key for a time at or below zero. | BL-490 |
| Redis:TTL-1 | MUST | `TTL`/`PTTL` answer `-2` for a missing key and `-1` for no expiry. | BL-490 |
| Redis:PERSIST-1 | MUST | `PERSIST` removes an expiry, answering 1, else 0. | BL-490 |
| Redis:TYPE-1 | MUST | `TYPE` answers `string`, `list`, `set`, `hash` or `none` as a simple string. | BL-490 |
| Redis:RENAME-1 | MUST | `RENAME` moves value and expiry; a missing source is `-ERR no such key`. | BL-490 |
| Redis:KEYS-1 | MUST | `KEYS` matches the page's glob patterns (`*`, `?`, `[…]`, `[^…]`, ranges, `\`): `h?llo` matches `hello` and `hallo`. | BL-490 |
| Redis:SCAN-1 | MUST | A full `SCAN` iteration from cursor 0 to 0 returns every key present throughout; `MATCH`, `COUNT` and `TYPE` filter; a bad cursor is `-ERR invalid cursor`. | BL-490 |
| Redis:DBSIZE-1 | MUST | `DBSIZE` counts the selected database; `FLUSHDB` empties it and `FLUSHALL` all 16. | BL-490 |
| Redis:Surl-Databases-1 | MUST | Databases are isolated: a key set in one is absent in another. | BL-490 |
| Redis:Surl-Persistence-1 | MUST | Nothing is written to disk; a new server starts with every database empty. | BL-490 |
| Redis:Surl-Limits-9 | MUST | A write past 1000000 keys or 256 MiB is `-OOM command not allowed when used memory > 'maxmemory'.`; reads and deletes still run. | BL-490 |

## Hashes, lists and sets

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Redis:WRONGTYPE-1 | MUST | A command on a key of another type is `-WRONGTYPE Operation against a key holding the wrong kind of value`. | BL-491 |
| Redis:HSET-1 | MUST | `HSET` answers the number of fields added; `HGET`, `HMGET`, `HDEL`, `HEXISTS`, `HLEN`, `HKEYS`, `HVALS`, `HINCRBY`, `HSETNX` their pages' replies. | BL-491 |
| Redis:HGETALL-1 | MUST | `HGETALL` is a map in RESP3 and a flat field-value array in RESP2. | BL-491 |
| Redis:LPUSH-1 | MUST | `LPUSH`/`RPUSH` answer the new length; `LLEN`, `LINDEX`, `LSET`, `LREM`, `LTRIM` their pages' replies. | BL-491 |
| Redis:LRANGE-1 | MUST | `LRANGE` takes negative indexes from the end: `LRANGE k 0 -1` is the whole list. | BL-491 |
| Redis:LPOP-1 | MUST | `LPOP`/`RPOP` without a count answer one element or null; with a count an array, or the null array on a missing key. | BL-491 |
| Redis:SADD-1 | MUST | `SADD`/`SREM` answer the number changed; `SISMEMBER`, `SMISMEMBER`, `SCARD`, `SPOP` their pages' replies. | BL-491 |
| Redis:SMEMBERS-1 | MUST | `SMEMBERS`, `SINTER`, `SUNION` and `SDIFF` are a set in RESP3 and an array in RESP2. | BL-491 |
| Redis:Surl-Collections-1 | MUST | A collection emptied by a command removes its key. | BL-491 |

## Pub/sub ("Pub/Sub" page)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Redis:PubSub-1 | MUST | `SUBSCRIBE` and `PSUBSCRIBE` confirm each channel or pattern with its name and the connection's count. | BL-491 |
| Redis:PubSub-2 | MUST | `UNSUBSCRIBE` and `PUNSUBSCRIBE` confirm each name left; with no argument they leave every one, or confirm a null name and count 0. | BL-491 |
| Redis:PubSub-3 | MUST | A `PUBLISH` on one connection reaches a subscriber on another as `message`, channel, payload, and a pattern subscriber as `pmessage`, pattern, channel, payload. | BL-491 |
| Redis:PubSub-4 | MUST | Confirmations and messages are arrays in RESP2 and push data in RESP3. | BL-491 |
| Redis:PubSub-5 | MUST | `PUBLISH` answers the number of subscriptions it reached. | BL-491 |
| Redis:PubSub-6 | MUST | In RESP2 a subscribed connection accepts only the subscribe commands, `PING`, `QUIT` and `RESET`; another command is the page's `-ERR Can't execute …` error, and `PING` answers the array `pong` and the message. | BL-491 |
| Redis:PubSub-7 | MUST | In RESP3 a subscribed connection may run any command. | BL-491 |
| Redis:Surl-Limits-10 | MUST | Past 10000 channels and patterns a connection, the subscribe is `-ERR max number of subscriptions reached`, nothing subscribed. | BL-491 |
| Redis:Surl-Limits-11 | MUST | A subscriber with more than 32 MiB of unsent messages is closed. | BL-491 |
| Redis:PubSub-8 | MUST | Channels join the shared bus (ADR-0100 decision 3.8), with `-NOPERM` for a denied channel (ADR-0102). | n/a: BL-678 moves pub/sub onto the bus after BL-491 and proves it |

## Scheme and transport

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Redis:URI-1 | MUST | `redis` serves on port 6379 by default. | n/a: the default port is the command line's (ADR-0083 decision 7), wired and tested by BL-492 |
| Redis:URI-2 | MUST | `rediss` carries the same protocol inside implicit TLS (TLS 1.2 or 1.3) from the first byte. | n/a: the TLS wrapper is the composition's `ImplicitTlsSchemeServer` (ADR-0010), added by BL-492, not the server's |
| Redis:URI-3 | MAY | The URI's user information and database path are the client's to send as `AUTH`/`HELLO` and `SELECT`. | n/a: a client-side rule; the server answers the commands (Redis:AUTH-1, Redis:SELECT-1) |
