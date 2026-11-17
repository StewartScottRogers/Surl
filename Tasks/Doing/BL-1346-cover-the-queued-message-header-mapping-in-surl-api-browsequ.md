---
id: BL-1346
title: Cover the queued-message header mapping in Surl.Api BrowseQueueAsync
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1346 — Cover the queued-message header mapping in Surl.Api BrowseQueueAsync

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Api` reports Surl.Api at 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result both times): Surl.Api 99.93% line,
  100% branch, 1 failing member.
- `<>c.<BrowseQueueAsync>b__7_2(BusMessageHeader)` - `Application/Surl.Api/SurlServerMessages.cs:98`, 0% line: the
  `header => KeyValuePair.Create(header.Name, header.Value)` lambda never runs, because no test browses a queued message
  that carries a header.

## Acceptance criteria

- [ ] A named `Surl.Api.UnitTests` test browses a queue holding a message with at least one header and asserts the
      header's name and value on the `SurlQueueMessage` returned.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Api` reports `Surl.Api | 100 | 100` with 0 failing members, and the fast
      tests are green.

## Notes

- Tests only (test-writer); no production code changes.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
