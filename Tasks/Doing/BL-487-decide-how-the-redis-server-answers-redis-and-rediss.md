---
id: BL-487
title: Decide how the Redis server answers redis and rediss
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-400]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Redis.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-487 — Decide how the Redis server answers redis and rediss

## Goal

An ADR decides how `surl redis://` and `rediss://` answer RESP2 and RESP3. It covers the commands served,
databases, persistence, authentication, errors, pub/sub and limits. A server-side requirement matrix traces each
decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - the Redis serialization protocol (https://redis.io/docs/latest/develop/reference/protocol-spec/: RESP2,
    RESP3, inline commands, pipelining);
  - the GitHub RESP3 specification (redis/redis-specifications `protocol/RESP3.md`);
  - the command reference pages for every command served, including `HELLO`, `AUTH` and `SELECT`;
  - the IANA provisional `redis` and `rediss` URI schemes.
- Which commands a client sends (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Redis.UnitLibrary`
  and its matrix `Redis.md`. The client sends one command per transfer, with `HELLO`, `AUTH` and `SELECT` from
  the URL. The port's `RedisReferenceServer` is copyable code.
- The folder: `Protocols/DataStore/` (BL-400's ADR).

Decide and record:

1. The command set. Planning assumption:
   - connection: `HELLO`, `AUTH`, `SELECT`, `PING`, `ECHO`, `QUIT`, `RESET`, `CLIENT SETNAME`/`GETNAME`/`ID`;
   - keys: `DEL`, `EXISTS`, `EXPIRE`/`PEXPIRE`, `TTL`/`PTTL`, `PERSIST`, `TYPE`, `RENAME`, `KEYS`, `SCAN`,
     `DBSIZE`, `FLUSHDB`;
   - strings: `GET`, `SET` with `EX`/`PX`/`NX`/`XX`/`GET`, `GETDEL`, `MGET`, `MSET`, `INCR`/`DECR`/`INCRBY`,
     `APPEND`, `STRLEN`;
   - hashes, lists and sets: the core commands;
   - pub/sub: `SUBSCRIBE`, `UNSUBSCRIBE`, `PUBLISH`, with RESP3 push messages.
   Every other command gets `-ERR unknown command`.
2. The RESP2/RESP3 switch through `HELLO`, and the reply type each command uses in each version.
3. Databases (the count), and whether data persists across restarts (under the content store's `.surl/` area, as
   mail does) or is in memory only.
4. Authentication: `AUTH` with a password, or a username and password, from the accounts file, required by
   default (`-NOAUTH` before it), with anonymous only under `--allow-anonymous`. `HELLO … AUTH`.
5. Error replies (`-ERR`, `-WRONGTYPE`, `-NOAUTH`, `-NOPROTO`, `-WRONGPASS`), with fixed text (ADR-0006 section 3).
6. Limits: the largest bulk string, the largest multibulk, the inline command length, and each ADR-0006 limit.
7. `rediss` over TLS.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its specification or command
      page, and is listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/Redis.md` exists in the matrix format, each "covered by" naming
      BL-488, BL-489, BL-490 or BL-491, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
