---
id: BL-1075
title: Find why upstream http test cases 30 and 37 still time out after an empty reply
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-683]
touches: []
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1075 — Find why upstream http test cases 30 and 37 still time out after an empty reply

## Goal

Upstream test cases 30 and 37 (curl 8.21.0) pass against Surl's HTTP server half, or the cause of their timeout is stated.

## Context

BL-683 found both cases still hit curl's 60 s limit after an empty reply (`<data>` empty, expected exit 52).
Cause and fix: ADR-0152 / BL-1071 (`HttpUpstreamTestCaseServer.cs`).

## Acceptance criteria

- [x] The cause of the timeout is stated under Notes.
- [x] Cases 30 and 37 are listed in `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt` and `UpstreamTestCasesAgainstSurlTests` passes.

## Notes

- 2026-11-12 (Claude, lane 1): cause: the HTTP half held the connection open on an empty `<data>` (BL-1078, for test 1548),
  so curl never saw the close and waited 60 s instead of exiting 52. BL-1071 (ADR-0152) closes after an empty reply on
  HTTP/1.1 (upstream's sws does the same) and holds only on HTTP/1.0. Both cases request HTTP/1.1, so the fix covers them.
  Verified: 30 and 37 are in the win-x64 passing list and `UpstreamTestCasesAgainstSurlTests` passed (1/1, 1m10s).
  No code change in this task.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Cases 30 and 37 pass: BL-1071's close-after-empty-reply fix (ADR-0152) already cured the timeout
