---
id: BL-1541
title: Send nothing when the client left during an HTTP/1.x command-stage delay hold
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1540]
touches: [Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1541 — Send nothing when the client left during an HTTP/1.x command-stage delay hold

## Goal

An HTTP/1.x request whose `delay` hold ended because the client left is not answered: nothing is written, the connection is closed (aborted after a reset), and a client that stays gets the same bytes as before.

## Context

ADR-0141 Amendment 3 decisions 1, 3 and 4. `HttpCommandFaults.cs:44` turns `Delay` into null; use the new helper result (BL-1540). HTTP/2 and HTTP/3 stay out. Tests: a scripted `InMemoryConnection` that half-closes during the hold records zero bytes sent; a staying client is answered unchanged.

## Acceptance criteria

- [ ] The goal holds, shown by named unit tests that fail without the change.
- [ ] dotnet build is clean, fast tests pass, 100% line and branch coverage of the changed library.

## Notes

## Log

- 2026-11-21: Created.
