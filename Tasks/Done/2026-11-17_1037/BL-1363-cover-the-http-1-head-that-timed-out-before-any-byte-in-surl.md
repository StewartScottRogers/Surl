---
id: BL-1363
title: Cover the HTTP/1 head that timed out before any byte in Surl.HttpServing
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.HttpServing.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1363 — Cover the HTTP/1 head that timed out before any byte in Surl.HttpServing

## Goal

`Measure-CodeQuality.ps1 -Library Surl.HttpServing.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.37% line, 99.31% branch, 1 failing member.
- `Http1RequestResponder.AnswerHeadNotReadAsync(HttpRequestHeadReadOutcome)` -
  `Protocols/Http/Surl.HttpServing.UnitLibrary/Http1RequestResponder.cs:66`, 81.25% line, 87.5% branch: the
  `HeadTimedOutBeforeAnyByte` branch at line 74 is never true, so lines 75, 76 and 78 (log the note, close with no
  bytes) never run.

## Acceptance criteria

- [x] A named `Surl.HttpServing.UnitTests` test has a connection send nothing until the head-read timeout passes on an
      injected `TimeProvider`, and asserts the note `No request head was read: HeadTimedOutBeforeAnyByte; closed with no
      bytes.` is logged and nothing is written to the connection.
- [x] `Measure-CodeQuality.ps1 -Library Surl.HttpServing.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Tests only (test-writer).

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. Added Http1ConnectionServerTests.ServeAsync_NoByteBeforeTheHeadTimeout_ClosesWithNoBytes; library 100|100, 0 failing
