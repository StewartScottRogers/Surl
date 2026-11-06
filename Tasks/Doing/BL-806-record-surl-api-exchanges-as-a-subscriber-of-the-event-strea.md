---
id: BL-806
title: Record Surl.Api exchanges as a subscriber of the event stream, with each exchange's listen URL and scheme
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-803]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-806 — Record Surl.Api exchanges as a subscriber of the event stream, with each exchange's listen URL and scheme

## Goal

`SurlServer.Exchanges` is filled by a subscriber of the instance's event stream instead of a recorder wired
beside the exchange log, and each `SurlExchangeRecord` now carries the listen URL and scheme it was served on, so
a test serving several listen URLs tells them apart and every later observer reads the same stream.

## Context

- Follow-up of BL-617 (Done): it built the record as the internal `RecordingExchangeLogFactory` passed to
  `Surl.Composition` through `FanOutExchangeLogFactory` (`Application/Surl.Api`), and ADR-0099 decision 7 recorded
  no listen URL because `IExchangeLogFactory.Create` gets none. BL-787's `ExchangeStartedEvent` carries the
  listen URL, which closes that gap.
- Decision: BL-822's ADR decision 1 (subscribers; amends ADR-0099 decision 7).
- Where: `Application/Surl.Api`. Subscribe to `ComposedServing`'s stream (BL-803) when `RecordExchanges` is on;
  build records from start, bytes and end events. Keep every public member BL-617 shipped unchanged in name and
  meaning - `SurlServerOptions.RecordExchanges`, `MaxRecordedExchanges` (1000), `MaxRecordedBytesPerExchange`
  (1 MiB), `SurlServer.Exchanges`, `DroppedExchangeCount`, `SurlExchangeRecord`, `SurlExchangeChunk`,
  `SurlExchangeDirection`, and `Exchanges` throwing when recording is off - and add `ListenUrl` (`Uri`) and
  `Scheme` to `SurlExchangeRecord` (additive: configuration and surface only grow). One chunk per read, write or
  datagram as before. Delete `RecordingExchangeLogFactory` and `FanOutExchangeLogFactory` once unused; the levelled
  log keeps its own `IExchangeLogFactory`.
- If the stream drops events for this subscriber past its bound (BL-787), the affected record is marked
  `IsTruncated` rather than silently wrong.
- Update `Application/Surl.Api/README.md`'s exchange-record section and `Application/Surl.Api/CLAUDE.md`.
- Stand-in: subscriber of the event stream (observable). `Surl.Mcp`'s `get_exchanges` follows in BL-809.

## Acceptance criteria

- [ ] Every existing test in `SurlServerTests.Exchanges.cs` passes with its assertions unchanged (the record's
      tests move to the new subscriber's test class, `<ClassName>Tests`).
- [ ] Named tests with `FakeListenerFactory` and `FakeConnection`: a server on `http://127.0.0.1:0` and
      `ftp://127.0.0.1:0` records each exchange with its own bound listen URL and scheme; the exchange-count and
      bytes-per-exchange caps behave as ADR-0099 decision 7 says.
- [ ] `RecordingExchangeLogFactory` and `FanOutExchangeLogFactory` no longer exist (or the Notes say why one stays).
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green (including
      `Application/Surl.Mcp.UnitTests`, which reads the record), and `Surl.Api` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
