---
id: BL-806
title: Record Surl.Api exchanges as a subscriber of the event stream, with each exchange's listen URL and scheme
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-803]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
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

- [x] Every existing test in `SurlServerTests.Exchanges.cs` passes with its assertions unchanged (the record's
      tests move to the new subscriber's test class, `<ClassName>Tests`).
- [x] Named tests with `FakeListenerFactory` and `FakeConnection`: a server on `http://127.0.0.1:0` and
      `ftp://127.0.0.1:0` records each exchange with its own bound listen URL and scheme; the exchange-count and
      bytes-per-exchange caps behave as ADR-0099 decision 7 says.
- [x] `RecordingExchangeLogFactory` and `FanOutExchangeLogFactory` no longer exist (or the Notes say why one stays).
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green (including
      `Application/Surl.Mcp.UnitTests`, which reads the record), and `Surl.Api` keeps 100% line and branch coverage.

## Notes

- Design (ADR-0124 decision 1, no new ADR needed): `ExchangeRecordSubscriber` subscribes to `ComposedServing.Events`
  with `IncludeBytes` at `SurlEventSubscriptionOptions.MaxCapacity` (the ADR: "a recorder that must lose nothing
  subscribes with a capacity to match"; the channel grows lazily) right after composing and before serving, and a
  pump task applies every event in stream order under a lock to `ExchangeRecordBook`, which holds the record logic
  (caps, truncation, chunks) and is unit tested without threads. It subscribes with no filter so it sees every
  sequence number: reading `Exchanges` or `DroppedExchangeCount` waits (Monitor.Wait) until the pump has applied
  through the stream's `LastSequence` at the moment of the read, or the pump has stopped. Without that wait a test
  that saw its connection close could read before the pump caught up, which the old synchronous recorder never
  allowed. Draining with `TryRead` from the reader instead was rejected: two consumers of one channel reorder events.
- Drops: when the subscription's `DroppedEventCount` grows (checked before and after each applied event), every
  record not yet ended is marked `IsTruncated`; it may over-mark, never under-mark. Bytes or an end for an exchange
  whose start was dropped are ignored, so such an exchange is not recorded at all (it cannot be named).
- `SurlExchangeRecord` gains `ListenUrl` (`Uri`, the event's reported listen URL, equal to the matching
  `BoundUrls` entry) and `Scheme` as positional parameters after `ExchangeId`. Every property BL-617 shipped is
  unchanged; the constructor is wider, which no user calls (Surl makes the records). `StartedAt` is now the
  started event's `At`, stamped by the stream from the same injected `TimeProvider`.
- Stopping disposes the subscription and awaits the pump (also on a failed bind), so `Exchanges` stays readable
  after `StopAsync`.
- `Surl.Api.csproj` now references `Surl.Core` directly (it uses `SurlEventStream`; it came transitively before).
- Touches widened to `Application/Surl.Mcp.UnitTests`: two test helpers construct `SurlExchangeRecord` and needed the
  two new arguments. No task in Doing touched it.
- `FakeListenerFactory` (linked into Surl.Mcp.UnitTests too) gains `ConnectionsToAcceptByScheme` so one server
  serves an http and an ftp exchange in one test.
- The old `RecordingExchangeLogFactoryTests` moved, one for one, to `ExchangeRecordBookTests` (the record logic);
  `ExchangeRecordSubscriberTests` covers the subscription, the catch-up wait, drops and disposal.
- Coverage: Surl.Api 100% line and branch. `Measure-CodeQuality.ps1` still flags the existing
  `SurlServerArguments.ThrowIfMisused` at Cobertura complexity 14 (CA1502 passes it in the build); not touched here.
## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Surl.Api records exchanges as a subscriber of the event stream, each record carrying its bound ListenUrl and Scheme
