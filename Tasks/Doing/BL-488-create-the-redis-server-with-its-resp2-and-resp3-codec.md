---
id: BL-488
title: Create the Redis server with its RESP2 and RESP3 codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-487, BL-404]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests, Surl.slnx, CLAUDE.md]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Both projects exist, are in `Surl.slnx` under `/Protocols/DataStore/`, and `ProtocolIsolationTests` passes.
- [ ] The root `CLAUDE.md`, "Repository layout", names `DataStore/` (Redis) among the `Protocols/` folders.
- [ ] Named tests encode each reply type to the specification's example bytes. They read a command split across
      reads, three pipelined commands, and an inline command. They refuse a bulk length over the limit, a
      non-numeric length, and a missing CRLF, as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
