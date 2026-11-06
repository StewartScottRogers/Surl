---
id: BL-807
title: Query recorded exchanges and events by protocol, listener, account and time, and wait for one, on a Surl.Api server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-806]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Documentation/Planning/Decisions/ADR-0143-how-surl-api-queries-and-waits-for-exchanges-and-events.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-807 — Query recorded exchanges and events by protocol, listener, account and time, and wait for one, on a Surl.Api server

## Goal

A test asks its `SurlServer` for the exchanges and events it cares about - by scheme, listen URL, account,
store, event kind and time - and can await the first one that matches with a timeout, so assertions such as "the
mail for bob arrived over SMTP within 5 seconds" are one line and never poll.

## Context

- Decision: BL-822's ADR decision 1 (which events `Surl.Api` keeps, their bound, the query's shape and the wait's
  failure). Planning surface: a query record (planning name `SurlEventQuery`: `Scheme`, `ListenUrl`, `Account`,
  `Kind`, `Since`, `Until`, `AfterSequence`), `SurlServer.FindExchanges(query)`, `SurlServer.FindEvents(query)`,
  `SurlServer.WaitForExchangeAsync(query, timeout, ct)` and `WaitForEventAsync(query, timeout, ct)`; a timed-out
  wait throws `TimeoutException` whose message says what was asked and how many near misses were seen. Public
  event types are `Surl.Api`'s own records mapped from the stream's, so the supported surface does not expose
  `Surl.Protocol.Abstractions` types unless the ADR says so.
- "By account" means the exchange in which that account's login was judged (BL-791's `LoginJudgedEvent`); "by
  time" uses `SurlServerOptions.TimeProvider`, and the wait's timeout runs on it too, so a test with a fake
  `TimeProvider` controls it. Tests use a hand-written fake `TimeProvider` (no package).
- A wait sees events that happened before it was called (it checks what is kept first), so a test cannot miss an
  event by awaiting too late.
- Where: `Application/Surl.Api`. Built on BL-806's subscriber. Update the package `README.md`.
- Stand-in: subscriber of the event stream; the assertion half of "observable".

## Acceptance criteria

- [x] Named tests (fake listeners and connections, no network): exchanges filtered by scheme, by listen URL, by
      account, and by time window each return exactly the matching ones; events filtered by kind and store; a wait
      for an event that already happened returns at once; a wait for a later event returns when it is published; a
      wait that times out on the fake `TimeProvider` throws `TimeoutException` with the ADR's message; a cancelled
      wait throws `OperationCanceledException`.
- [x] Named tests show the event bound applied as the ADR says, and a query with recording off throwing as
      `Exchanges` does.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

- Decisions recorded in ADR-0143 (Decided by Claude under Stewart's delegation): queries ride the one
  `RecordExchanges` subscription, so every query and wait throws as `Exchanges` does with recording off; events
  are the stream's own `SurlEvent` records (ADR-0124 decision 5 already puts `SurlEvent` on `Surl.Api`'s
  surface, and a mapped copy would need editing for every new event kind); `SurlEventQuery` gains `Store`
  (the acceptance asks for events by store); `Since` inclusive, `Until` exclusive; an exchange matches `Kind`
  and `Store` when one of its events does; the timeout message counts the non-byte events kept while waiting.
- Added the ADR file and the Decisions README row to `touches`: an ADR was needed for the decisions, and no task
  in Doing names either file.
- Tests: `SurlEventFactsTests`, `ExchangeFactsTests`, `EventRecordBookTests`, `ExchangeRecordBookTests`
  (`FindExchanges_ByAccountAndStore_…`, `FactsOf_…`), `ExchangeRecordSubscriberTests.Queries` (by scheme, listen
  URL, account, time window; events by kind and store; event bound; waits already-happened, later, timed out on
  `ManualTimeProvider`, cancelled), `SurlServerTests.Queries` (end-to-end over fake HTTP and FTP connections,
  recording off, null query) and the non-positive `MaxRecordedEvents` row. `Surl.Api` 100% line and branch,
  0 failing members (`Measure-CodeQuality.ps1 -Library Surl.Api`); 156 tests.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. SurlServer.FindExchanges/FindEvents/WaitForExchangeAsync/WaitForEventAsync query recorded exchanges and events by SurlEventQuery, waits time out on the injected TimeProvider
