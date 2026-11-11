---
id: BL-771
title: Trace the Redis hash, list, set and pub/sub requirements to BL-491's tests and record its two error texts
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-491]
touches: [Documentation/Conformance/Extended/Redis.md, Documentation/Planning/Decisions/ADR-0106-how-the-redis-server-answers-redis-and-rediss.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-771 — Trace the Redis hash, list, set and pub/sub requirements to BL-491's tests and record its two error texts

## Goal

The Redis requirement matrix names the test proving each row BL-491 delivered, and ADR-0106 decision 5's
error table lists the two error texts BL-491 added.

## Context

- BL-491 built hashes, lists, sets and pub/sub in `Surl.Extended.Protocol.Redis.UnitLibrary`, but its
  `touches` could not include `Documentation/Conformance` (BL-394 held it) or `Documentation/Planning/Decisions`
  (BL-563 held it), so the matrix rows still read `BL-491`.
- Tests: `RedisHashCommandsTests`, `RedisListCommandsTests`, `RedisSetCommandsTests`, `RedisPubSubTests`,
  `RedisReplyWriterTests` in `Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests`.
- Error texts BL-491 answers that decision 5's table lacks, both Redis's own words (`RedisErrorReplies`):
  `-ERR hash value is not an integer` (`HINCRBY` on a field whose value is not an integer) and
  `-ERR value is out of range, must be positive` (a negative count to `LPOP`, `RPOP`, `SPOP`).
- `SPOP` takes the member added first rather than a random one (BL-491's Notes); the ADR may say so beside
  "`KEYS`, `SMEMBERS`, `HGETALL` and the like answer in insertion order".

## Acceptance criteria

- [x] Every `Documentation/Conformance/Extended/Redis.md` row reading `BL-491` (WRONGTYPE-1, HSET-1, HGETALL-1,
      LPUSH-1, LRANGE-1, LPOP-1, SADD-1, SMEMBERS-1, Surl-Collections-1, PubSub-1 to PubSub-7, Surl-Limits-10,
      Surl-Limits-11), and the notes on RESET-1 and TYPE-1, names the test that proves it.
- [x] ADR-0106 decision 5's table has rows for `hash value is not an integer` and
      `value is out of range, must be positive`, marked as added by BL-491 on 2026-11-05.

## Notes

- 2026-11-11: Named the BL-491 tests on every listed row of `Documentation/Conformance/Extended/Redis.md`, choosing for each row the tests whose asserted bytes show the behaviour (several where a row covers several commands). PubSub-8 still reads BL-678, which is outside this task. RESET-1 now names `RedisPubSubTests.Reset_LeavesEveryChannelAndPattern`; TYPE-1 names `RedisHashCommandsTests.Type_HashListAndSet_AreNamedByType`.
- 2026-11-11: ADR-0106 decision 5 gains the two error rows, checked against `RedisErrorReplies.HashValueNotAnInteger` and `RedisErrorReplies.NotPositive`; decision 1 now says `SPOP` takes the member added first.
- 2026-11-11: Markdown only, no `.cs` or project file changed, so the verify skill does not apply. `dotnet build` could not run on this machine: `global.json` pins SDK 10.0.401, which is not installed (BL-1035). Nothing this task changed is compiled.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Redis matrix rows BL-491 delivered name their proving tests; ADR-0106 lists BL-491's two error texts and SPOP's order
