---
id: BL-617
title: Record what each client sent and was sent on a Surl.Api server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-616]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-617 — Record what each client sent and was sent on a Surl.Api server

## Goal

A test that opts in can read, from its `SurlServer`, every exchange the server has served - its listen URL, the
client's address, when it started, and the bytes received and sent in the order they crossed the connection - so it
can assert on exactly what its client sent. The record is bounded and costs nothing when not asked for.

## Context

- Decision: BL-611's ADR, decision 7 (the opt-in property, the record's public types, and its bounds: exchanges
  kept, bytes per exchange, and what happens past each). Use its names.
- Every byte a protocol server reads or writes already reaches `IExchangeLog`
  (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/IExchangeLog.cs`) through `Surl.Core`'s `RecordingConnection`
  and `RecordingDatagramFlow` (ADR-0004 section 5), created per exchange by an `IExchangeLogFactory`. The record is
  an `IExchangeLogFactory` in `Surl.Api` given to `Surl.Composition`'s start (BL-614) beside, not instead of, the
  log the options ask for.
- Exchanges finish on the engine's tasks while the test reads: the record must be safe to read while serving
  (`.claude/rules/testing.md`: tests run method-level parallel).
- Bounds follow ADR-0006's style: a cap, then a stated behaviour (oldest dropped or bytes cut, as the ADR says),
  never unbounded growth.

## Acceptance criteria

- [ ] Named tests drive the record's `IExchangeLogFactory` directly (no network) and show: received and sent bytes
      kept in order for one exchange; two exchanges kept apart with their listen URL and client address; a datagram
      exchange (TFTP) recorded; the bytes-per-exchange cap and the exchange-count cap each applied as the ADR says.
- [ ] A named test with a fake listener factory and a fake connection shows a server started with recording on
      exposes, through the ADR's public member, the exact bytes the fake client wrote; and one with recording off
      exposes none.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
