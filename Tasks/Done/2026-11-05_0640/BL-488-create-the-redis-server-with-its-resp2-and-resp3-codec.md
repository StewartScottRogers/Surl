---
id: BL-488
title: Create the Redis server with its RESP2 and RESP3 codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-487, BL-404]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests, Surl.slnx, CLAUDE.md, Documentation/Conformance/Extended/Redis.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-488 — Create the Redis server with its RESP2 and RESP3 codec

## Goal

`Surl.Extended.Protocol.Redis.UnitLibrary` exists in the new `Protocols/DataStore/` folder. It reads commands
(arrays of bulk strings, and inline commands) and writes every RESP2 and RESP3 reply type, with pipelined commands
read one by one.

## Context

- Decisions: BL-487's ADR, decisions 2, 5 and 6, and BL-400's ADR (the folder). Rules: BL-543's ADR.
- ADR-0095 (BL-400) leaves the root `CLAUDE.md`, "Repository layout", to this task: its tree gains `DataStore/` under
  `Protocols/` once the folder exists, so the tree lists only folders that exist.
- New projects in `Protocols/DataStore/`. `Surl.slnx` gains the `/Protocols/DataStore/` solution folder in ordinal
  order, as ADR-0079 decision 4 says. Namespace `Surl.Extended.Protocol.Redis`. References:
  `Surl.Protocol.Abstractions` only.
- The RESP specification's examples are the tests' bytes: simple strings, errors, integers, bulk strings and the
  null bulk string, arrays and the null array (RESP2); null, boolean, double (including `inf` and `nan`), big
  number, bulk error, verbatim string, map, attribute, set and push (RESP3).

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx` under `/Protocols/DataStore/`, and `ProtocolIsolationTests` passes.
- [x] The root `CLAUDE.md`, "Repository layout", names `DataStore/` (Redis) among the `Protocols/` folders.
- [x] Named tests encode each reply type to the specification's example bytes. They read a command split across
      reads, three pipelined commands, and an inline command. They refuse a bulk length over the limit, a
      non-numeric length, and a missing CRLF, as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Plan: the codec is one `RespValue` subclass per RESP2/RESP3 type, each writing its own prefix (`Encode()`); the
  reader is `RedisCommandReader` over `IConnection` with a growing buffer, returning `RedisCommandReadOutcome`
  (command, protocol error with its `-ERR Protocol error: ...` reply, too large, closed). No server class: BL-489
  creates the `IConnectionProtocolServer` with the connection commands, so head timeout, the max-clients refusal
  and writing the error before closing (Redis:Surl-Limits-7, -8) moved to BL-489 in `Redis.md` and its Context.
- Choice: `Documentation/Conformance/Extended/Redis.md` added to `touches` to replace this task's ID with the
  named tests, as the matrix asks; no task in Doing names it.
- Choice: a value is written exactly as built; picking `$-1` or `_`, array or set by protocol version is the
  caller's (BL-489 onwards), so the codec has no version state.
- Choice: header numbers are read as Redis's `string2ll`: optional minus, `0` alone or no leading zero, 64-bit;
  `+1`, `01` and `-0` are invalid. A `*` or `$` line longer than `--max-line` is the invalid length of its kind
  (Redis says "too big mbulk count string"; the ADR's table has only the two texts).
- Choice: inline splitting follows Redis's `sdssplitargs`: leading white space is C `isspace` (space, tab, CR,
  LF, VT, FF), an unquoted argument ends at space, tab, CR or LF; a closing quote must be followed by white space.
- Choice: `expected '$', got '<byte>'` writes the byte itself, CR or LF as a space (ADR-0106 decision 5).
- Choice: the bulk bound is `ExchangeLimits.MaxUploadBytes` capped at `Array.MaxLength - 2`; the command bound
  is `MaxUploadBytes` + 64 KiB (`RedisCommandReader.CommandFramingAllowance`).
- Choice: every type's specification example is in one class, `RespValueEncodingTests`, as the CoAP library
  keeps its RFC examples together; the types share one encoding helper.
- Verified: `dotnet build -warnaserror` clean; fast tests green (Redis 114); the library at 100% line and branch
  coverage (cobertura from the MSTest collector); `ProtocolIsolationTests` passes.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Redis library encodes every RESP2 and RESP3 reply type and reads array, inline and pipelined commands with ADR-0106's protocol errors
