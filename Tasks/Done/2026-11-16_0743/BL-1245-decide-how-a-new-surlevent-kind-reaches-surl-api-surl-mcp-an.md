---
id: BL-1245
title: Decide how a new SurlEvent kind reaches Surl.Api, Surl.Mcp and the control endpoint without editing a switch in each
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-060
created: 2026-11-16
completed: 2026-11-16
---
# BL-1245 — Decide how a new SurlEvent kind reaches Surl.Api, Surl.Mcp and the control endpoint without editing a switch in each

## Goal

An ADR, decided by Claude under Stewart's delegation, says how a `SurlEvent` record added to `Surl.Protocol.Abstractions` is queried, filtered and written by all three hosts without a hand edit in each, or, if a hand edit stays, which test fails until it is made. The follow-up tasks are filed.

## Context

- Found by the second stand-in audit (2026-11-16). Root `CLAUDE.md`, "Open for extension": new protocols, stores, faults and hooks plug into extension points, "never into a central switch that must be edited".
- Today each host switches on the event's type, and an unknown type falls through silently:
  - `Application/Surl.Api/SurlEventFacts.cs`: `KindOf` (`_ => null`, so `FindEvents` with a `Kind` never matches it), `ExchangeIdOf`, `OwnSchemeOf`; and the `SurlEventKind` enum (`Application/Surl.Api/SurlEventKind.cs`).
  - `Application/Surl.Control.UnitLibrary/ControlEventKinds.cs` (`Known`, `ExchangeIdOf`) and `ControlEventWriter.cs` (`_ => head`, so the event's own members are not written).
  - `Application/Surl.Mcp.UnitLibrary/EventToolFilter.cs` (the hand-written `kinds` enum in the input schema) and `RecordedEventWriter.cs` (`ExchangeIdOf`, the member switch).
- `ConsumerGroupChangedEvent` (ADR-0145 decision 10, BL-946) needed all of these edited by hand. `StoreKind` already has a guard (`Application/Surl.Mcp.UnitTests/StoreEventFilterTests.Schema_StoreEnum_ListsEveryStoreKindInSnakeCase`); event kinds have none in any host's tests.
- Options to weigh: a member on the `SurlEvent` base (an `ExchangeId` and a `Scheme` the subclasses override; a kind name derived from the type) so the switches go; writing members generically with source-generated `System.Text.Json` (AOT-safe, no reflection at run time); or keeping the switches with completeness tests that enumerate `typeof(SurlEvent).Assembly`'s exported subclasses, as `Foundation/Surl.Protocol.Abstractions.UnitTests/SurlEventContractTests.EventRecordTypes` does. Base class library only; AOT-safe.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/`, with the next free number, marked "Decided by Claude under Stewart's delegation", picks one option with its reason and the rejected ones, and amends ADR-0124 decision 2 ("Extension points") with a line for event kinds.
- [x] One task per library the ADR changes is filed with `task-board.ps1 new`, depending on this task, with exact `touches` (a contract task in `Surl.Protocol.Abstractions.UnitLibrary` first, if any). Each is listed in Notes.
- [x] Each host follow-up's criteria name a test that fails when an exported `SurlEvent` subclass is not queryable, filterable and written in full by that host.

## Notes

ADR-0171 decided: base members plus WriteMembers, guard tests per host. Follow-ups: BL-1283 (Abstractions contract, first), BL-1284 (Surl.Api), BL-1285 (control), BL-1286 (Surl.Mcp), each depending on this task; BL-1284 to 1286 also on BL-1283.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ADR-0171 written; follow-ups BL-1283 to BL-1286 filed
