---
id: BL-490
title: Answer Redis keyspace and string commands with expiry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-489]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Named tests show:
      - each listed command's documented replies in RESP2 and RESP3 (for example `GET` of a missing key is `$-1`
        in RESP2 and `_` in RESP3);
      - `SET k v PX 100` expiring after the clock advances 100 ms;
      - `TTL` of `-2` and `-1`;
      - `INCR` overflow refused;
      - `SCAN` returning every key across calls;
      - `KEYS h?llo` matching as the page says;
      - `SELECT` isolating databases;
      - the persistence behaviour the ADR chose.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
