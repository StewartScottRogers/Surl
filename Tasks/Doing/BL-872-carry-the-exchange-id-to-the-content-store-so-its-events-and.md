---
id: BL-872
title: Carry the exchange ID to the content store so its events and fault points name the exchange
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-789, BL-1035]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-872 — Carry the exchange ID to the content store so its events and fault points name the exchange

## Goal

A content-store `StoreChangedEvent`, `FaultAppliedEvent` and `FaultPoint` carry the exchange that made
the change, so a test can tie an upload's store change to the FTP or HTTP exchange that sent it.

## Context

- ADR-0129 decision 2: `ExchangeId` is `null` on every content change because the store's API takes a
  `ContentPathMapping` built from a `ContentCaller`, which carries an account and a scheme but no exchange.
- Planned shape: `ContentCaller` gains an optional exchange ID (for example `ForAccount(name, scheme)`
  and `Anonymous(scheme)` with a `WithExchange(long)` or an `init` property), and
  `ContentStoreEventsAndFaults` puts `mapping.Caller.ExchangeId` on the event and the fault point.
  Record the choice as an amendment to ADR-0129.
- Each file server then passes `ExchangeContext.ExchangeId` when it builds its caller; file one task per
  server family for that wiring, depending on this one.

## Acceptance criteria

- [ ] Named `ContentStoreTests` show a write, delete, rename, folder change and out-of-space refusal made
      through a caller with exchange ID 7 publish events with `ExchangeId` 7 and ask the plan with
      `ExchangeId` 7; a caller with none still publishes `null`.
- [ ] `dotnet build` is clean, the fast tests are green, and `Surl.Content.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

- 2026-11-11 (lane 3): Code and tests written but not built: `dotnet build` fails because the pinned
  .NET SDK 10.0.401 (`global.json`) is not installed - same as BL-813 and BL-849. Returned to Backlog
  behind BL-1035; the uncommitted work is in the shift's stash.
- Design taken: `ContentCaller.ExchangeId` (`long?`, null from `ForAccount` and `Anonymous`) and
  `ContentCaller.WithExchange(long)` returning a new caller. `ContentStoreEventsAndFaults.PublishChange`
  takes the exchange ID; store changes pass `mapping.Caller.ExchangeId` (a rename the source's), and
  seeds, test deletes, reset and restore pass null. `RunsOutOfSpaceAsync` puts `caller.ExchangeId` on the
  `FaultPoint` and the `FaultAppliedEvent`. Tests: `ContentCallerTests.WithExchange_*` and
  `ForAccountAndAnonymous_NameNoExchange`, `ContentStoreTests.Changes_ThroughACallerOnExchange7_*`,
  `Upload_OutOfSpaceThroughACallerOnExchange7_*` and `Upload_OutOfSpaceThroughACallerOnNoExchange_*`;
  the library's CLAUDE.md updated.
- The ADR-0129 amendment is outside `touches`, and BL-671 in Doing holds `Documentation/Planning/Decisions`;
  file it as a docs task when this lands, with the per-server wiring tasks.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-12: Backlog -> Doing.
