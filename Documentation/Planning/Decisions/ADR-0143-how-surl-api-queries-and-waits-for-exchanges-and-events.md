# ADR-0143 — How Surl.Api queries and waits for exchanges and events

- **Status:** Accepted
- **Date:** 2026-11-06
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in BL-807.
- **Details:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 1, "`Surl.Api`'s queries", which
  named `SurlEventQuery`, `FindExchanges`, `FindEvents`, `WaitForExchangeAsync`, `WaitForEventAsync`,
  `MaxRecordedEvents` (10000) and the `TimeoutException`, and left their details to BL-807.

## Context

A test that uses Surl as a stand-in asserts on what happened: "bob's mail arrived over SMTP within five
seconds", "the FTP client logged in as alice". `SurlServer.Exchanges` (BL-806) gave every exchange's
bytes, but finding the right one meant a hand-written filter, and waiting for one meant polling.
ADR-0124 fixed the surface; this ADR fixes what each part means.

## Decision

1. **One subscription.** The queries ride `ExchangeRecordSubscriber`, the subscription `RecordExchanges`
   already makes; it now feeds an `EventRecordBook` beside the `ExchangeRecordBook`. So every query and wait
   needs `RecordExchanges` and, with it off, throws the same `InvalidOperationException` as `Exchanges`. A
   second subscription would cost a second copy of every event for no gain.
2. **The events are the stream's own records.** `FindEvents` and `WaitForEventAsync` return
   `Surl.Protocol.Abstractions`' `SurlEvent` records, not copies mapped into `Surl.Api` types. ADR-0124
   decision 5 already puts `SurlEvent` on `Surl.Api`'s surface (`OnEvent`'s callback), the package bundles
   the assembly, and a mapped copy would have to be edited with every new event kind, against "open for
   extension". `ExchangeBytesEvent` is never kept: bytes live on the exchange records.
3. **What is kept.** At most `SurlServerOptions.MaxRecordedEvents` events (10000 by default, positive, else
   `ArgumentException`), oldest dropped and counted in `SurlServer.DroppedEventCount`. Exchanges stay bounded
   by `MaxRecordedExchanges` as before.
4. **The query.** `SurlEventQuery` is an `init`-only record; every property left at its default matches
   anything, every one given must match:
   - `Scheme` (case-insensitive): the exchange's; for an event, a judged login's own scheme, else its
     exchange's.
   - `ListenUrl` (`Uri`, as `BoundUrls` lists it): the exchange's, or the event's exchange's.
   - `Account` (case-sensitive): an exchange in which a login for it was judged, accepted or refused
     (`LoginJudgedEvent`); an event that names it (that login, an `Accounts` store change of it), or any
     event of such an exchange.
   - `Kind` (`SurlEventKind`, one member per kept event record): the event's kind; an exchange matches when
     one of its events was of that kind.
   - `Store` (`StoreKind`): a `StoreChangedEvent` of that store; an exchange in which one happened. ADR-0124
     listed no `Store`; BL-807's acceptance asks for events filtered by store, so it is added.
   - `Since` inclusive and `Until` exclusive, against an event's `At` or an exchange's start, so adjacent
     windows never share a moment.
   - `AfterSequence`: an event's `Sequence`, or the sequence of an exchange's `ExchangeStartedEvent`.
   An event whose exchange record was dropped has no scheme, listen URL or exchange accounts, so it matches
   no query that names them.
5. **The wait.** `WaitFor…Async(query, timeout, cancellationToken)` first looks at what is kept, so an event
   that came before the call is found at once; else each applied event wakes it to look again, and the
   first match wins (an exchange may match while still open, and its record is the snapshot at that moment).
   The timeout is a `CancellationTokenSource` on `SurlServerOptions.TimeProvider`, so a fake clock controls
   it; `Timeout.InfiniteTimeSpan` waits for ever, a negative timeout is `ArgumentOutOfRangeException`. On
   timeout it throws `TimeoutException`: `No event matching SurlEventQuery { … } within 00:00:05; N events
   were recorded while waiting and none matched.` (`exchange` for an exchange wait), N counting the
   non-byte events kept after the wait began. Cancellation throws `OperationCanceledException`, also when
   the token was already cancelled and a match was kept.

## Consequences

- One line asserts a cross-protocol fact without polling, and a test with a fake `TimeProvider` controls
  every wait's timeout.
- `Surl.Mcp` (BL-809), the control endpoint and hooks (BL-811) keep their own subscriptions; BL-811's
  `OnEvent(SurlEventQuery, …)` reuses this query and its meaning.
- A new event record needs a `SurlEventKind` member and a line in `SurlEventFacts` to be found by kind;
  until then it is kept and found by every other property.
