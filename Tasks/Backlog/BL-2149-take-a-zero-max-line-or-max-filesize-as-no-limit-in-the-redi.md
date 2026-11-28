---
id: BL-2149
title: Take a zero --max-line or --max-filesize as no limit in the Redis server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: NFR-014
created: 2026-11-28
completed:
---
# BL-2149 — Take a zero --max-line or --max-filesize as no limit in the Redis server

## Goal

With `--max-line 0` or `--max-filesize 0` the `redis` and `rediss` server holds no limit of that kind, as ADR-0006 section 1 says ("0 means no limit"). This task changes nothing in `Documentation/Product/Requirements.md`; BL-2157 re-states NFR-014 to NFR-016.

## Context

Found while working BL-2120 (2026-11-28). In `Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary/RedisCommandReader.cs` the constructor takes the limits as given:

- `maxLineBytes = limits.MaxLineBytes`, and `ReadLineAsync` refuses `lineLength > maxLineBytes`, so 0 refuses every line.
- `maxBulkBytes = Math.Min(limits.MaxUploadBytes, Array.MaxLength - 2)`, so 0 refuses every non-empty bulk string.
- `maxCommandBytes = limits.MaxUploadBytes + CommandFramingAllowance`, so 0 holds a whole command to 65536 bytes, and a value near `long.MaxValue` overflows to a negative bound.

With no `--max-filesize` limit a bulk string is still bounded by what one array holds. The limits Redis puts on a client that has not logged in (`AppliesUnauthenticatedLimits`, ADR-0106 decision 6) stay as they are. Apply the same guard to any other use of these two limits in the project. Stand-in: a test that turns a limit off to push a large value through `surl` gets it stored; nothing new to seed, reset or publish.

## Acceptance criteria

- [ ] A test in `RedisCommandReaderTests` reads an inline command and a multibulk command with `MaxLineBytes` 0, each with a line longer than 8192 bytes.
- [ ] A test in `RedisCommandReaderTests` reads, with `MaxUploadBytes` 0, a command whose one bulk string is longer than 65536 bytes.
- [ ] A test in `RedisCommandReaderTests` reads a command with `MaxUploadBytes` `long.MaxValue`, so the whole-command bound does not overflow.
- [ ] A test shows the unauthenticated bulk and multibulk bounds still refuse when both limits are 0.
- [ ] The existing tests for non-zero limits still pass, `dotnet build Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests -warnaserror` is clean, and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
