---
id: BL-1364
title: Cover the Redis key tool branches in Surl.Mcp.UnitLibrary
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1364 — Cover the Redis key tool branches in Surl.Mcp.UnitLibrary

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Mcp.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.37% branch, 3 failing
  members, branch-only, in `Application/Surl.Mcp.UnitLibrary/`:
  - `SetKeyTool.NameOf(int, byte[])` - `SetKeyTool.cs:77`, 87.5% branch; line 97 is 1 of 2: a name longer than
    `MaxNameCharacters` (256) is never truncated, or never under it.
  - `ListKeysTool.ReadAsync` (async) - `ListKeysTool.cs:46`, 83.33% branch; line 50 is 1 of 2: the `database` argument
    is only ever absent or only ever given.
  - `<ReadAsync>b__0(Utf8JsonWriter)` in `ReadKeyTool.cs:69`, 75% branch; line 72 is 3 of 4: `read.Bytes is { } bytes`
    misses one outcome (a key with no bytes, such as a missing key or a non-string type).

## Acceptance criteria

- [ ] A named `Surl.Mcp.UnitTests` test sets a key whose name (database prefix, decoded runes and `\xNN` escapes) passes
      256 characters and asserts the event name is cut to exactly 256; another asserts a short name is kept whole.
- [ ] Named tests call `list_keys` with and without `database`, asserting the database read.
- [ ] Named tests call `read_key` for a key with bytes and one without, asserting the JSON has and lacks the bytes member.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Mcp.UnitLibrary` reports `100 | 100` with 0 failing members, and the fast
      tests are green.

## Notes

- Tests only (test-writer). Tool names as registered in `Surl.Mcp.UnitLibrary`.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
