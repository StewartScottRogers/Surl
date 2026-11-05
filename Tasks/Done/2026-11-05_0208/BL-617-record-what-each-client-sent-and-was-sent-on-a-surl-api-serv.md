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
completed: 2026-11-05
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

- [x] Named tests drive the record's `IExchangeLogFactory` directly (no network) and show: received and sent bytes
      kept in order for one exchange; two exchanges kept apart with their listen URL and client address; a datagram
      exchange (TFTP) recorded; the bytes-per-exchange cap and the exchange-count cap each applied as the ADR says.
- [x] A named test with a fake listener factory and a fake connection shows a server started with recording on
      exposes, through the ADR's public member, the exact bytes the fake client wrote; and one with recording off
      exposes none.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

- Delivered as ADR-0099 decision 7 names it: `SurlServerOptions.RecordExchanges`, `MaxRecordedExchanges` (1000),
  `MaxRecordedBytesPerExchange` (1 MiB); `SurlServer.Exchanges` and `DroppedExchangeCount`; public
  `SurlExchangeRecord`, `SurlExchangeChunk`, `SurlExchangeDirection`. Internal `RecordingExchangeLogFactory` (the
  record) and `FanOutExchangeLogFactory` (passes every call to the levelled/trace log, then the record).
- Listen URL: ADR-0099 decision 7 already decided a record carries none (`IExchangeLogFactory.Create` gets no
  listener, and widening the shared contract for one host was rejected), departing from this task's Goal. The first
  criterion is met on exchange ID and client address; `Create_TwoExchanges_KeepsThemApartWithTheirIdsAndClientsOldestFirst`.
- Defaults taken (the ADR was silent): one chunk per read, write or datagram (no merging, so datagram boundaries stay
  visible); a write crossing the byte cap keeps the bytes up to the cap, then nothing more; `DroppedExchangeCount`
  throws with recording off, as `Exchanges` does, so a test cannot read a silent 0; `NoteOutsideExchange` and
  `Note` record nothing (decision 7: notes are `Log`'s).
- Tests: `RecordingExchangeLogFactoryTests` (8, incl. a TFTP RRQ/DATA/ACK datagram exchange and both caps),
  `FanOutExchangeLogFactoryTests` (1), `SurlServerTests.Exchanges.cs` (6 runs: an upstream-curl-8.21.0-shaped GET on a
  `FakeConnection` handed out once by `FakeListenerFactory.ConnectionToAccept`, recording off, the bound checks, the
  defaults). Surl.Api.UnitTests 101 green; Surl.Api 100% line and branch.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. SurlServerOptions.RecordExchanges records each exchange's received and sent bytes, bounded, readable as SurlServer.Exchanges
