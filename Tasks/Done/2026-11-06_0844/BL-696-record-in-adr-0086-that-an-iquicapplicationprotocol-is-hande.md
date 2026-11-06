---
id: BL-696
title: Record in ADR-0086 that an IQuicApplicationProtocol is handed a QuicApplicationConnection, and add the Abstractions reference to ADR-0002's Surl.Quic row
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-572]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-696 — Record in ADR-0086 that an IQuicApplicationProtocol is handed a QuicApplicationConnection, and add the Abstractions reference to ADR-0002's Surl.Quic row

## Goal

ADR-0086 decision 4 and ADR-0002 decision 3's table say what BL-572 built: `IQuicApplicationProtocol.ServeAsync`
takes a `QuicApplicationConnection`, and `Surl.Quic.UnitLibrary` references `Surl.Protocol.Abstractions.UnitLibrary`.

## Context

- ADR-0086 decision 4 names `ServeAsync(QuicServerConnection connection, ...)` as a planning name and says the
  building task may name the type differently. BL-572 handed the application a `QuicApplicationConnection`
  instead: `QuicDatagramFlowServer`'s loop receives datagrams and acts on timers while the application reads and
  writes, and `QuicServerConnection` is not thread-safe, so every application call goes through one lock. See
  `Protocols/Http/Surl.Quic.UnitLibrary/CLAUDE.md` and BL-572's Notes for the semantics (the application returning
  closes with code 0; the connection ending cancels `Ended`).
- BL-572 could not edit `Documentation/Planning/Decisions` itself: BL-642 held it at the time.
- ADR-0086 decision 5 already gives the `Surl.Quic` row; check ADR-0002's own table carries it.

## Acceptance criteria

- [x] ADR-0086 decision 4 (or a short amending note in it) names `QuicApplicationConnection` as what
      `IQuicApplicationProtocol.ServeAsync` receives, and says why (one lock between the loop and the application).
- [x] ADR-0002 decision 3's table lists `Surl.Protocol.Abstractions.UnitLibrary` in `Surl.Quic.UnitLibrary`'s row,
      or points to ADR-0086 decision 5 for it.

## Notes

Filed by BL-572.

- 2026-11-06: ADR-0086 decision 4 now shows `ServeAsync(QuicApplicationConnection connection, ...)` and a paragraph saying why (one lock between the flow loop and the application; `QuicServerConnection` is not thread-safe), checked against `IQuicApplicationProtocol.cs` and `Surl.Quic.UnitLibrary/CLAUDE.md`.
- ADR-0002 already carried it: its decision 3 table row for `Surl.Quic.UnitLibrary` (ADR-0082, ADR-0086) lists `Surl.Protocol.Abstractions.UnitLibrary`, and its Amended line for ADR-0086 says so, so ADR-0002 needed no edit.
- Docs only, no `.cs` or project file changed, so the `verify` skill was not needed; build and fast tests run anyway as the lane rule asks.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0086 decision 4 names QuicApplicationConnection as what IQuicApplicationProtocol.ServeAsync receives and why; ADR-0002's Surl.Quic row already lists Abstractions
