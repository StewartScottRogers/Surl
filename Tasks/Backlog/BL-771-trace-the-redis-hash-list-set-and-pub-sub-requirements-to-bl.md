---
id: BL-771
title: Trace the Redis hash, list, set and pub/sub requirements to BL-491's tests and record its two error texts
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-491, BL-1035]
touches: [Documentation/Conformance/Extended/Redis.md, Documentation/Planning/Decisions/ADR-0106-how-the-redis-server-answers-redis-and-rediss.md]
requirement: none
created: 2026-11-05
completed:
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

- [ ] Every `Documentation/Conformance/Extended/Redis.md` row reading `BL-491` (WRONGTYPE-1, HSET-1, HGETALL-1,
      LPUSH-1, LRANGE-1, LPOP-1, SADD-1, SMEMBERS-1, Surl-Collections-1, PubSub-1 to PubSub-7, Surl-Limits-10,
      Surl-Limits-11), and the notes on RESET-1 and TYPE-1, names the test that proves it.
- [ ] ADR-0106 decision 5's table has rows for `hash value is not an integer` and
      `value is out of range, must be positive`, marked as added by BL-491 on 2026-11-05.

## Notes

- 2026-11-11 (lane 4): The documentation work is finished and sits uncommitted in the lane (cherry-picked from factory/BL-771-lane-1-20261111-060243: every listed Redis.md row names its test, ADR-0106 decision 5 has both error rows, decision 1 says SPOP takes the member added first). `dotnet build` cannot run: global.json pins SDK 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. The task waits on BL-1035, as BL-767 and BL-993 do. When it restarts, re-apply the branch and move to Done once the build is green.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 1 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-771-lane-1-20261111-060243; start with git cherry-pick --no-commit factory/BL-771-lane-1-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; the docs work is on factory/BL-771-lane-1-20261111-060243
- 2026-11-11: Backlog -> Doing.
- 2026-11-12: Doing -> Backlog. returned by the shift starting 20261112-052315: it was in Doing but no stopped lane held it; look for its work in 'git stash list' and in factory/BL-771-* branches before redoing it
