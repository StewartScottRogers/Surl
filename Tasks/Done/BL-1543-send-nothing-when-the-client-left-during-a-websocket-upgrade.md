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
completed: 2026-11-21
---
# BL-1543 — Send nothing when the client left during a WebSocket upgrade delay hold

## Goal

A WebSocket upgrade whose `delay` hold ended because the client left sends no `101` and no frame; a staying client gets the same bytes as before.

## Context

ADR-0141 Amendment 3. Uses the `HttpCommandFaults` change of BL-1541 (WebSocketUpgradeResponder.Faults.cs).

## Acceptance criteria

- [x] The goal holds, shown by named unit tests that fail without the change.
- [x] dotnet build is clean, fast tests pass, 100% line and branch coverage of the changed library.

## Notes

The upgrade aborts the connection and sends nothing when the hold ended by client close or reset, as BL-1541 does for HTTP/1.x. Tests: DelayRuleAtTheCommandStage_EndsWhenTheClientHalfCloses... and _SendsNothingWhenTheClientResetsDuringTheHold.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. WebSocket upgrade sends nothing when client left during a delay hold
