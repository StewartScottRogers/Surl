---
id: BL-1435
title: Withhold Redis AUTH and HELLO AUTH passwords from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1435 — Withhold Redis AUTH and HELLO AUTH passwords from the exchange log

## Goal

`surl -v --user alice:s3cret --self-signed rediss://127.0.0.1:0/` never logs the password of `AUTH` or
`HELLO ... AUTH`, in RESP arrays or inline commands; it is written as BL-1417's ADR says.

## Context

- Bug report item: the parent named Redis `AUTH`. Redis is an extended server (ADR-0083), judged by its
  protocol's specification; there is no upstream curl client for it.
- `Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary/RedisServer.cs` is the server;
  `RedisConnectionCommands.cs:23` registers `auth` (`AuthAsync`, `:115`, `AUTH [username] password`)
  and `:214` reads `HELLO`'s `AUTH username password`. Clients send RESP arrays
  (`*2\r\n$4\r\nAUTH\r\n$6\r\ns3cret\r\n`), so the redactor follows array and bulk-string lengths
  across chunks. Inline commands (`AUTH s3cret\r\n`) use BL-1418's line redactor. Command names are
  case-insensitive.

## Acceptance criteria

- [ ] `RedisServer` implements BL-1418's contract. Fast tests in
      `Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests` show RESP `AUTH s3cret`,
      `AUTH alice s3cret` and `HELLO 3 AUTH alice s3cret` logged with the password in the ADR's form,
      also split across reads.
- [ ] Inline `auth s3cret` is redacted, and `SET k s3cret` is logged unchanged (named tests).
- [ ] `dotnet build Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary -warnaserror` is clean,
      the fast tests are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
