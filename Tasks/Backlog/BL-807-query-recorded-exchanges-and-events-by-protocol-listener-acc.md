---
id: BL-807
title: Query recorded exchanges and events by protocol, listener, account and time, and wait for one, on a Surl.Api server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-806]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-05
completed:
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

- [ ] Named tests (fake listeners and connections, no network): exchanges filtered by scheme, by listen URL, by
      account, and by time window each return exactly the matching ones; events filtered by kind and store; a wait
      for an event that already happened returns at once; a wait for a later event returns when it is published; a
      wait that times out on the fake `TimeProvider` throws `TimeoutException` with the ADR's message; a cancelled
      wait throws `OperationCanceledException`.
- [ ] Named tests show the event bound applied as the ADR says, and a query with recording off throwing as
      `Exchanges` does.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
