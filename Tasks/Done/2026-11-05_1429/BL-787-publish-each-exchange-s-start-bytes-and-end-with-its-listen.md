---
id: BL-787
title: Publish each exchange's start, bytes and end with its listen URL on a bounded event stream in Surl.Core
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-787 — Publish each exchange's start, bytes and end with its listen URL on a bounded event stream in Surl.Core

## Goal

`Surl.Core` holds the event stream implementation, and the serving engine publishes, for every connection and
datagram exchange, its start (exchange ID, listen URL, scheme, client, time), the bytes received and sent in
order, and its end, so any subscriber sees every exchange of every protocol without the protocol doing anything.

## Context

- Decision: BL-822's ADR decision 1 (ordering, sequence numbers, bound per subscriber and what happens past it,
  whether bytes are published with no subscriber - planning default: not).
- Contracts: BL-823 (`ISurlEventPublisher`, the event records, `ExchangeContext.Events`).
- Where: `Foundation/Surl.Core.UnitLibrary`. The stream (planning name `SurlEventStream`, implementing
  `ISurlEventPublisher`) offers subscriptions; publishing never blocks or throws into a server. `ServingEngine`
  (`ServingEngine.cs`, `ServingEngine.Datagrams.cs`) takes the publisher (null object when not given, so existing
  callers are unchanged), sets `ExchangeContext.Events`, and publishes start and end. Bytes: the engine already
  wraps each exchange in `RecordingConnection` and `RecordingDatagramFlow`, which call `IExchangeLog`; publish the
  bytes from there (or from an `IExchangeLog` that forwards to the publisher), copying the span, as
  `IExchangeLog`'s remarks require. The levelled log and `--trace` keep using `IExchangeLog` unchanged.
- Time comes from the engine's injected `TimeProvider`; no wall clock. No static state: two engines with two streams
  never see each other's events.
- This closes ADR-0099 decision 7's gap: the engine knows the listener, so `ExchangeStartedEvent` carries the
  listen URL a record could not.
- Stand-in: this is the "observable" principle's publish point for exchanges; stores publish their own changes
  (BL-788 to BL-791).

## Acceptance criteria

- [x] Named `SurlEventStreamTests` show: events delivered in publish order with increasing sequence numbers; two
      subscribers each see every event; a slow subscriber past its bound loses events as the ADR says and reports
      how many, while publishing never blocks (no `Thread.Sleep`; drive with a fake `TimeProvider`); a disposed
      subscription receives nothing more.
- [x] Named `ServingEngineTests` with fake listeners and `InMemoryConnection`/`InMemoryDatagramFlow` show: a TCP
      exchange publishes start (with its `ListenUrl` and scheme), the exact received and sent bytes in order, and
      end; a TFTP-style datagram exchange the same; two listeners' exchanges carry their own listen URLs; with no
      publisher given, behaviour and log output are unchanged.
- [x] `dotnet build Foundation/Surl.Core.UnitLibrary -warnaserror` is clean, the whole solution builds, the fast
      tests are green, and `Surl.Core` keeps 100% line and branch coverage.

## Notes

- Built as ADR-0124 decision 1 says: `SurlEventStream` (one lock stamps `Sequence` and `At` and offers the
  event to every subscription), `SurlEventSubscription` (bounded channel, `DropOldest`, `DroppedEventCount`,
  `ReadAllAsync`, plus `TryRead` for draining without waiting) and `SurlEventSubscriptionOptions`
  (`Capacity` default 65536, 1 to 1048576; `IncludeBytes`; `Filter`, the ADR's "optional predicate").
- Choices with a sensible default, taken here:
  - A filter that throws is taken as refusing the event, so `Publish` never throws into a server.
    `Publish(null)` still throws `ArgumentNullException`, as `RecordingSurlEventPublisher` does: that is a
    caller bug, not a subscriber's.
  - Disposing a subscription stops delivery at once; events already buffered stay readable and
    `ReadAllAsync` ends after them. Disposing twice is harmless.
  - The engine takes the publisher through a new ninth constructor parameter; the eight-parameter
    constructor passes `DiscardingSurlEventPublisher.Instance`, so every existing caller is unchanged.
  - Bytes are counted and published by an internal `EventPublishingExchangeLog` that wraps the exchange log
    and is handed to `RecordingConnection`, `RecordingDatagramFlow`, `ExchangeDataConnections` and the first
    datagram, so FTP data-connection bytes are published too, in the order the log sees them. The server's
    `ExchangeContext.Log` stays the log itself, so log output and identity are unchanged.
  - `ExchangeEndedEvent` is published in a `finally`, so an exchange that published its start always
    publishes its end, even when the log throws. The start is published after the "opened" note, so an
    exchange whose log could not be created or noted publishes nothing.
  - The listen URL is formatted as `surl`'s "Listening on" line does (`ListenerStatusLine.FormatBoundListenUrl`
    in `Surl.Output`), by a small internal `ServingEngine.FormatListenUrl`: `Surl.Core` does not reference
    `Surl.Output` and this task does not touch it. A Unix-socket listen URL has no bound port, so it carries
    the port asked for.
- Not wired into `Surl.Composition` here: BL-803 creates one stream per `ComposedServing` and passes it in.
- Measured: `Surl.Core.UnitTests` 164 passed; Surl.Core.UnitLibrary 100% line, 100% branch, max complexity
  10 (Measure-CodeQuality.ps1); whole solution builds with 0 warnings; fast tests 17546 passed across 69
  projects.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ServingEngine publishes every exchange's start (listen URL, scheme, client), bytes in order and end on SurlEventStream, a bounded drop-oldest per-subscriber stream
