---
id: BL-1349
title: Cover the control target and control serving gaps in Surl.Composition
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1349 — Cover the control target and control serving gaps in Surl.Composition

## Goal

None of the five `ComposedControlTarget` and `ComposedServing` members listed below appears as failing in
`Measure-CodeQuality.ps1 -Library Surl.Composition.UnitLibrary`.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): Surl.Composition.UnitLibrary 99.6%
  line, 99.43% branch, 12 failing members. This task takes the five behind the control endpoint; BL-1350 takes the
  other seven and closes the library.
- `ComposedControlTarget.ReadLogAsync(string, long, int, CancellationToken)` - `ComposedControlTarget.cs:309`, 40% line,
  0% branch; lines 311, 313, 314 uncovered, and its record lambda `<ReadLogAsync>b__36_0` (line 311) never runs. No
  test reads a Kafka log through the composed control target.
- `<BrowseQueueAsync>b__34_1(BusMessageHeader)` - `ComposedControlTarget.cs:294`, 0% line: no browsed message carries a header.
- `ComposedControlTarget.IsBoundOrDuplicate(DnsZoneFileException)` - `ComposedControlTarget.cs:368`, 50% branch: only one
  of the two refusals ("past the zones' bounds", "a second zone") is ever seen (ADR-0158 decision 9).
- `ComposedServing.ServeWithControlAsync` (async) - `ComposedServing.cs:194`, 66.67% branch; the partial line is 229
  (`await feed;`, 6 of 10), so the event feed has always finished by the time it is awaited.
- `<ServeWithControlAsync>b__32_1(ListenUrl)` - `ComposedServing.cs:200`, 0% line: the `_ => { }` callback handed to the
  control listener's `ListenerStartReporter` is never called.

## Acceptance criteria

- [ ] A named `Surl.Composition.UnitTests` test reads a topic log with at least one record through
      `ComposedControlTarget.ReadLogAsync` and asserts each record's offset, publish time, key and payload, and the page's
      next offset and has-more flag (lines 311, 313, 314).
- [ ] A named test browses a queued message carrying a header through `ComposedControlTarget.BrowseQueueAsync` and
      asserts the header (line 294).
- [ ] Named tests drive both `IsBoundOrDuplicate` refusals through the zone routes and assert each answer.
- [ ] A named test reaches the `ComposedServing.cs:200` callback, and one reaches line 229 with the feed still running,
      or, if either is unreachable by construction, the code is reshaped so no unreachable edge remains and the Notes say why.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Composition.UnitLibrary` lists none of the five members above, and the fast
      tests are green.

## Notes

- Tests first (test-writer). Production code changes only for an edge that no test can reach, as BL-1055 did.

## Log

- 2026-11-17: Created.
