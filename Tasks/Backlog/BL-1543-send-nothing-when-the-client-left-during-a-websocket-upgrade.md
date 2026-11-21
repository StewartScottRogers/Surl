---
id: BL-1543
title: Send nothing when the client left during a WebSocket upgrade delay hold
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1541]
touches: [Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1543 — Send nothing when the client left during a WebSocket upgrade delay hold

## Goal

A WebSocket upgrade whose `delay` hold ended because the client left sends no `101` and no frame; a staying client gets the same bytes as before.

## Context

ADR-0141 Amendment 3. Uses the `HttpCommandFaults` change of BL-1541 (WebSocketUpgradeResponder.Faults.cs).

## Acceptance criteria

- [ ] The goal holds, shown by named unit tests that fail without the change.
- [ ] dotnet build is clean, fast tests pass, 100% line and branch coverage of the changed library.

## Notes

## Log

- 2026-11-21: Created.
