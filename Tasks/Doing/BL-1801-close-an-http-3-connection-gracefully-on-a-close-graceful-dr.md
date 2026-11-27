---
id: BL-1801
title: Close an HTTP/3 connection gracefully on a close=graceful drop fault, as ADR-0188 says
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1794]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Core.UnitLibrary, Surl.Core.UnitTests, Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Surl.Quic.UnitLibrary, Surl.Quic.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-26
completed:
---
# BL-1801 — Close an HTTP/3 connection gracefully on a close=graceful drop fault, as ADR-0188 says

## Goal

An `Accept`-stage drop with `after-bytes` and `close=graceful` on an HTTP/3 listener closes the QUIC connection with
`CONNECTION_CLOSE` and no error after byte N, and its `FaultAppliedEvent` says `Graceful`.

## Context

Found while planning BL-1794. ADR-0188's HTTP/3 row promises the QUIC transport's graceful close, but
`Foundation/Surl.Core.UnitLibrary/ServingEngine.Datagrams.cs` wraps every datagram flow, QUIC included, in
`SilencedAfterBytesDatagramFlow` and ignores `FaultRule.Close`, so an HTTP/3 connection is only silenced and its event
says `Reset` (ADR-0188, amendment 4). Stand-in use: a test cuts an HTTP/3 download cleanly and asserts what the client
saw. If the work is too big for one run, split it.

## Acceptance criteria

- [ ] A graceful `after-bytes` rule on HTTP/3 sends `CONNECTION_CLOSE` with no error after the N bytes, pinned by a unit
      test over an in-memory datagram flow.
- [ ] That drop's `FaultAppliedEvent` has `Close = FaultClose.Graceful`.
- [ ] TFTP and every other plain datagram flow still publish `FaultClose.Reset`, pinned by a test.
- [ ] ADR-0188 has an amendment recording the change.
- [ ] What pinned upstream curl does with the cut is measured and recorded there, or the amendment says no pinned build
      speaks HTTP/3.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
