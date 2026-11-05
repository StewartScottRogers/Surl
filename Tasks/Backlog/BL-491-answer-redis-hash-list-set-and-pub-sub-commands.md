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
completed:
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

- [ ] Named tests show:
      - each listed command's documented replies in RESP2 and RESP3;
      - `HGETALL` as a RESP3 map and as a RESP2 array;
      - `LRANGE k 0 -1`;
      - `-WRONGTYPE` for `LPUSH` on a string;
      - an emptied list removing its key;
      - a `PUBLISH` from one connection delivered to a subscriber on another, as a push in RESP3 and an array in
        RESP2;
      - a non-pub/sub command while subscribed in RESP2 refused as the page says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
