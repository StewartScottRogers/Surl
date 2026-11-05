---
id: BL-490
title: Answer Redis keyspace and string commands with expiry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-489]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests, Documentation/Conformance/Extended/Redis.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-490 — Answer Redis keyspace and string commands with expiry

## Goal

The Redis server keeps a keyspace per database. It answers the key commands and string commands the ADR lists,
with expiry on an injected `TimeProvider`, and persists or not as the ADR decides.

## Context

- Decision: BL-487's ADR, decisions 1 and 3.
- Command pages for each command served:
  - `SET` options and their replies: `+OK`, the null reply on a failed `NX`/`XX`, and the old value with `GET`;
  - `INCR` on a non-integer (`-ERR value is not an integer or out of range`) and on overflow;
  - `EXPIRE` and `TTL` (`-2` for a missing key, `-1` for no expiry);
  - `SCAN`'s cursor contract;
  - `KEYS` glob patterns.
- If the data persists, it is written through `Surl.Content`'s `IContentFileSystem` (tests use a
  `UnitTest`-prefixed fake).

## Acceptance criteria

- [x] Named tests show:
      - each listed command's documented replies in RESP2 and RESP3 (for example `GET` of a missing key is `$-1`
        in RESP2 and `_` in RESP3);
      - `SET k v PX 100` expiring after the clock advances 100 ms;
      - `TTL` of `-2` and `-1`;
      - `INCR` overflow refused;
      - `SCAN` returning every key across calls;
      - `KEYS h?llo` matching as the page says;
      - `SELECT` isolating databases;
      - the persistence behaviour the ADR chose.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- **Plan.** ADR-0106 already decides the command set, replies, errors, expiry, `SCAN` cursor,
  databases and persistence, so no new ADR was needed. The protocol-architect stage was skipped for
  that reason; the work was built straight from the ADR and the command pages, then checked by
  `code-reviewer`.
- **What was built.** `RedisKeyspace` (16 databases per server, shared by its connections, counted
  against 1000000 keys / 256 MiB), `RedisDatabase`, `RedisKeyEntry`, `RedisStoredValue` /
  `RedisStringValue`, `RedisKeyCommands` (15 commands), `RedisStringCommands` (11 commands),
  `RedisGlobPattern`, `RedisSetOptions` / `RedisSetExpiry`, `RedisExpireConditions`,
  `RedisScanOptions`, `RedisExpiryTimes` and `RedisKeyspaceCommand` (the per-command lock and `-OOM`
  check). `RedisCommandCall` now carries the keyspace and the clock's time.
- **Persistence (ADR-0106 decision 3):** in memory only. `IContentFileSystem` is not used.
  `Server_NewServer_StartsWithEveryDatabaseEmpty` shows that a new server starts empty.
- **Defaults taken (ADR-0106 leaves them open):**
  - A key is absent once `now >= expiry`. Redis uses `now > expiry`, but Redis.md's SET-3 row says
    the key is absent once the clock has advanced 100 ms, so `>=` was chosen. As a result `PTTL`
    never answers 0.
  - Keys are held as Latin-1 strings, one character per byte. This makes byte equality, hashing and
    glob matching exact without a custom comparer.
  - `-OOM` is checked before a growing command runs, as Redis checks `maxmemory`. The growing
    commands are `SET`, `MSET`, `INCR`, `DECR`, `INCRBY`, `DECRBY` and `APPEND`. One write can
    therefore pass the bound by one value; that value is itself capped by `--max-filesize`. Before
    refusing, expired keys are swept from all 16 databases (a fix from review).
  - `RENAME` is not a growing command. This matches Redis's command flags.
  - `DECRBY` with the least 64-bit integer answers ADR-0106's
    `-ERR increment or decrement would overflow`, not Redis's separate `decrement would overflow`
    text, because the ADR's error table is the fixed list.
  - `SCAN`'s cursor accepts digits only, up to the largest unsigned 64-bit value. `TYPE` compares
    without regard to case, and an unknown type matches nothing.
  - Glob matching backtracks to the last `*`, so its cost is bounded by pattern length × key
    length. It follows Redis's matcher in the corner cases: a backwards range is swapped, an
    unclosed class runs to the end of the pattern, and a trailing `\` is a literal.
- **TYPE-1** is tested with `string`, `none` and a test-only `hash` value (`UnitTestRedisValue`).
  The real `list`, `set` and `hash` types arrive with BL-491.
- **Touches:** added `Documentation/Conformance/Extended/Redis.md` so its BL-490 rows could be
  replaced by the named tests. No task in Doing names that file.
- **Quality:** the library has 100% line and 100% branch coverage. `CA1502` is clean at build. The
  coverage tool's own cyclomatic-complexity figure is above 10 for `Set`, `WithFlag`,
  `RedisExpireConditions.TryRead`, `MatchesOne` and `IsMatch`. Those figures count IL branches,
  including string-switch hashing; earlier members of this library such as `ReadEscape` and
  `ReadBulkStringAsync` are over 10 on the same measure.
- **Tests:** the Redis test project runs 308 tests. All 61 fast-test assemblies pass.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Redis server keeps 16 shared in-memory databases and answers the key and string commands with expiry on the injected clock, KEYS globs, SCAN cursors and the -OOM bound
