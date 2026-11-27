---
id: BL-1794
title: Carry the close kind on a drop fault's FaultAppliedEvent
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Core.UnitLibrary, Surl.Core.UnitTests, Surl.Control.UnitLibrary, Surl.Control.UnitTests, Surl.Mcp.UnitTests, Documentation/Planning/Decisions/ADR-0188-a-drop-fault-cuts-a-transfer-with-a-graceful-close-after-n-bytes-through-the-close-key.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1794 — Carry the close kind on a drop fault's FaultAppliedEvent

## Goal

The `FaultAppliedEvent` a drop fault publishes at stage `Accept` carries how the connection was closed (`Reset` or
`Graceful`), in `Surl.Api`, in `surl-mcp`'s event JSON and in the control endpoint's event JSON, so a test can assert
a cut was clean without reading the `-v` log.

## Context

Bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-26T2130-Surl-bug-report.md`, item 2 (Low, seen through
`Surl.Api` on `1e5204d`, the code of v0.1.9; not re-run on v0.1.10 `654b8d2cb`). Repro, copied from the report:

> Surl.Api, an `http://` server with `Faults = { SurlFault.DropConnection() with { AfterBytes = 1_000_000, Close = FaultClose.Graceful } }`, a file larger than that, one fetch; then the same with `FaultClose.Reset`.
> - `server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied })` returns one `FaultAppliedEvent` each time, with the same fields both ways: `RuleId f1, Kind Drop, Stage Accept`.
> - Expected: the close in the event, such as a `Close` property (Graceful or Reset), and the same in surl-mcp's `fault_applied` event and the control endpoint's events.

This is by design today: ADR-0188 decision 3 says the graceful cut publishes "the same single `FaultAppliedEvent` as a
drop" and only the exchange-log note differs. This task changes that decision, so it amends ADR-0188 (decided by
Claude under Stewart's delegation: the stand-in rule "Observable" wants every exchange's outcome on the event stream,
and "Configuration as data" lets events grow by a field).

Design, fixed here so the run need not decide it:
- `Foundation/Surl.Protocol.Abstractions.UnitLibrary/FaultAppliedEvent.cs`: add an init-only property
  `public FaultClose? Close { get; init; }`, **not** a positional parameter, so none of the ~45 construction sites
  across the protocol libraries changes. Null means the event is not an engine `Accept`-stage drop. `WriteMembers`
  writes it last with `writer.Choice(nameof(Close), close)` only when it is not null. No existing member is renamed,
  re-typed or reordered (fields only grow).
- `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs`: `PublishFaultApplied` (reached from `NoteAndPublishDrop` and
  `NoteAndPublishAfterBytes`) sets `Close`: `FaultClose.Graceful` for the graceful after-bytes cut,
  `FaultClose.Reset` for the reset after-bytes drop and for the drop "before any byte" (which always aborts).
- `Application/Surl.Control.UnitLibrary/ControlEventMemberWriter.cs` throws on a member it has no place for, so add
  `["Close"]` to `ChoiceMembers` and a `Close` string property to `ControlEventJson` in `ControlJson.cs` (and its
  source-generated JSON context if one lists members). It is written `close`, snake case like the rest (ADR-0136);
  null is left out.
- `surl-mcp` needs no production change: `Application/Surl.Mcp.UnitLibrary/RecordedEventWriter.cs` writes any `Choice`
  as snake case, so it appears as `"close":"graceful"`; only a test is added.
- `Surl.Api` needs no change: `FindEvents` returns the `Surl.Protocol.Abstractions.FaultAppliedEvent` itself.
- Command-stage drops (`CommandStageFaults` and the per-protocol `*Faults.cs` files) leave `Close` null; they do not
  read `FaultRule.Close` today. Say so in the ADR amendment.

Stand-in: no store is involved. The change is on the instance's event stream only: one more member on the existing
`FaultApplied` event, seen by every subscriber (`Surl.Api` queries, `surl-mcp`'s `get_events` / `wait_for_event`, the
control endpoint's events). Seeding, reset and fault configuration are unchanged.

## Acceptance criteria

- [ ] `FaultAppliedEvent` has `FaultClose? Close { get; init; }`; a test in `Surl.Protocol.Abstractions.UnitTests`
      pins that `WriteMembers` writes `Close` when set and writes no `Close` member when null, with every other member
      unchanged.
- [ ] Regression tests in `Surl.Core.UnitTests` (beside the existing graceful-close drop tests) pin `Close` on the
      published event: `Graceful` for a `Close = FaultClose.Graceful` after-bytes rule, `Reset` for a reset after-bytes
      rule, and `Reset` for a drop before any byte - so the two rules in the repro no longer publish identical events.
- [ ] A test in `Surl.Control.UnitTests` pins the control event JSON of a graceful drop's event with `"close":"graceful"`,
      and one of a non-drop `FaultAppliedEvent` with no `close` member.
- [ ] A test in `Surl.Mcp.UnitTests` (`RecordedEventWriterTests`) pins `"close":"graceful"` on a `fault_applied` event.
- [ ] ADR-0188 has an amendment, marked "Decided by Claude under Stewart's delegation", stating that the `Accept`-stage
      drop event carries `Close` (`Reset` or `Graceful`), that command-stage drop events leave it null, and that
      decision 3's "same event" no longer holds; the log notes are unchanged.
- [ ] `dotnet build -warnaserror` is clean for the three changed libraries, `dotnet test --filter "TestCategory!=Integration"`
      passes, and each changed library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
