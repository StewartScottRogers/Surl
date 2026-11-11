---
id: BL-745
title: Close a DTLS association with close_notify before its 48-bit sequence number wraps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1035]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-745 — Close a DTLS association with close_notify before its 48-bit sequence number wraps

## Goal

An established `DtlsServerSession` whose next epoch 1 sequence number would be the last one (2^48 - 1)
sends a warning `close_notify` with it and ends the association, instead of `SendAsync` throwing
`DtlsSequenceNumberExhaustedException` (ADR-0087 decision 8).

## Context

- ADR-0087 decision 8: "an association whose next sequence number would wrap is closed with `close_notify`
  instead" (RFC 6347 section 4.1).
- `DtlsRecordWriter` refuses once 2^48 - 1 is used; `DtlsAssociationRecords.StartProtectedWrites` builds the
  epoch 1 writer from 0. A test needs a way to start that writer near the end, internal to the library.
- `DtlsServerSession.CloseAsync` (BL-415) already sends `close_notify` and ends the association.
- Found by BL-415, which added PSK, the alerts and `close_notify` but not this case.

## Acceptance criteria

- [ ] A named test in `Surl.Dtls.UnitTests` shows an association whose epoch 1 writer is at its last sequence
      number answers `SendAsync` by sending `close_notify` with that number, then `ReceiveAsync` returns `null`
      and a further `SendAsync` throws `InvalidOperationException`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
