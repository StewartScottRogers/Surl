---
id: BL-491
title: Answer Redis hash, list, set and pub/sub commands
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-490]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-491 — Answer Redis hash, list, set and pub/sub commands

## Goal

The Redis server answers the hash, list and set commands the ADR lists, with `-WRONGTYPE` for a key of another
type. It answers pub/sub with RESP2 message arrays and RESP3 push messages, if BL-487's ADR serves pub/sub.

## Context

- Decision: BL-487's ADR, decision 1.
- Command pages for each command served:
  - `HSET`/`HGET`/`HGETALL` (a map in RESP3, a flat array in RESP2);
  - `LPUSH`/`RPUSH`/`LRANGE` with negative indexes, `LPOP` with a count;
  - `SADD`/`SMEMBERS` (a set in RESP3);
  - `SUBSCRIBE`/`PUBLISH`, and "Pub/Sub" (the subscribe confirmation, message delivery, and the commands
    allowed while subscribed in RESP2).
- `-WRONGTYPE Operation against a key holding the wrong kind of value` is the reply text the documentation gives.

## Acceptance criteria

- [x] Named tests show:
      - each listed command's documented replies in RESP2 and RESP3;
      - `HGETALL` as a RESP3 map and as a RESP2 array;
      - `LRANGE k 0 -1`;
      - `-WRONGTYPE` for `LPUSH` on a string;
      - an emptied list removing its key;
      - a `PUBLISH` from one connection delivered to a subscriber on another, as a push in RESP3 and an array in
        RESP2;
      - a non-pub/sub command while subscribed in RESP2 refused as the page says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Tests (all in `Surl.Extended.Protocol.Redis.UnitTests`): `RedisHashCommandsTests` (HGETALL as RESP2 array and
  RESP3 map: `HGetAll_IsAFlatArrayInRespTwoAndAMapInRespThree`), `RedisListCommandsTests` (`LRANGE k 0 -1`:
  `LPushRPush_AnswerTheNewLengthAndLRangeZeroToMinusOneIsTheWholeList`; `-WRONGTYPE` for LPUSH on a string:
  `LPush_OnAString_IsWrongTypeAndLeavesTheString`; emptied list loses its key:
  `LPopRPop_WithACount_AnswerAnArrayAndAnEmptiedListLosesItsKey`), `RedisSetCommandsTests`, `RedisPubSubTests`
  (cross-connection delivery: `Publish_ToASubscriberOnAnotherConnection_ArrivesAsAnArrayInRespTwo` and
  `..._ArrivesAsAPushInRespThree`; RESP2 refusal: `SubscribedInRespTwo_OtherCommandsAreRefusedAndPingIsAnArray`),
  `RedisReplyWriterTests`. 363 tests in the project; library 100% line and 100% branch (Measure-CodeQuality.ps1).
- Design: collections (`RedisCollectionValue`) are changed in place and recounted by `RedisDatabase.Recount`,
  which removes an emptied key, rather than copied per change as strings are - a copy per `LPUSH` would make
  building a list quadratic. `RedisStoredValue`'s doc now says so.
- Pub/sub: every byte a connection writes goes through one `RedisReplyWriter` (a one-at-a-time gate), so a
  message another connection publishes never cuts into a reply. The publisher does not wait for delivery; past
  ADR-0106's 32 MiB unsent, or on a failed write, the writer cancels the connection's token and the connection
  closes with no more bytes. Subscriptions are left when the connection ends and on `RESET`.
- Defaults taken: `SPOP` takes the member added first (the page picks at random, which no client can tell
  from a fixed choice, and tests can pin bytes). `UNSUBSCRIBE`/`PUNSUBSCRIBE` with no argument leave in
  subscription order. The RESP2 subscribed-context check comes after the login check, as in Redis.
- Error texts beyond ADR-0106 decision 5's table, Redis's own words: `-ERR hash value is not an integer`
  (HINCRBY on a non-integer field) and `-ERR value is out of range, must be positive` (negative LPOP/RPOP/SPOP
  count). `LPOP`/`RPOP` with more than one count argument is the arity error and `SPOP` the syntax error, as Redis.
  Recording them in the ADR and tracing `Redis.md` to the tests needs `Documentation/Planning/Decisions`
  (held by BL-563) and `Documentation/Conformance` (held by BL-394), so they are filed as BL-771.
- Measure-CodeQuality still flags seven pre-existing methods (BL-488/BL-490) over complexity 10 by its IL
  count; the build's CA1502 gate passes. None is new in BL-491.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Redis answers hash, list and set commands with -WRONGTYPE and emptied-key removal, and pub/sub across connections in RESP2 arrays and RESP3 pushes
