---
id: BL-1542
title: Send nothing when the client left during an RTSP command-stage delay hold
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1541]
touches: [Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1542 — Send nothing when the client left during an RTSP command-stage delay hold

## Goal

An RTSP request whose `delay` hold ended because the client left is not answered and the session ends; a staying client gets the same bytes as before.

## Context

ADR-0141 Amendment 3. Uses the `HttpCommandFaults` change of BL-1541 (RtspRequestResponder.Faults.cs).

## Acceptance criteria

- [ ] The goal holds, shown by named unit tests that fail without the change.
- [ ] dotnet build is clean, fast tests pass, 100% line and branch coverage of the changed library.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
