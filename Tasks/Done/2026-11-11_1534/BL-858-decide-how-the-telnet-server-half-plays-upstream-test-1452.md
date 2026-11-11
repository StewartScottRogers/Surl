---
id: BL-858
title: Decide how the TELNET server half plays upstream test 1452
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Conformance]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-858 — Decide how the TELNET server half plays upstream test 1452

## Goal

An ADR decides whether upstream test 1452 (`telnet://%HOSTIP:%TELNETPORT --upload-file -`, stdin
`test1452`, `<stdout nonewline="yes">test1452</stdout>`) is played by a TELNET server half or
listed as a `test-server-choice` exclusion, and files the work it needs.

## Context

- Found in BL-396's run (Windows, reference build, 2026-11-05): the suite composes Surl's own
  `TelnetProtocolServer` for `telnet` (the case's `<data>` is empty), and 1452 fails `timeout`
  after 60 s. Surl's server (ADR-0025) sends a banner, echoes each line with CRLF and closes only
  after `quit`; curl uploads stdin and waits for the server to close. Upstream's test server
  negotiates, echoes what it read without a line ending and closes.
- Options: a `TelnetUpstreamTestCaseServer` in `Surl.Protocol.Telnet` (ADR-0080 decision 2's
  payload mode) with no banner that closes after echoing; or an exclusion row under ADR-0080
  decision 5, since the banner, the echo's line ending and the close are upstream's choices.
  Measure upstream curl's own exit and output against Surl's server first.

## Acceptance criteria

- [x] An ADR (or an amendment to ADR-0080) records the decision, marked "Decided by Claude under
      Stewart's delegation".
- [x] The work it needs is filed as tasks, or the exclusion row is in
      `Documentation/Conformance/Upstream-Test-Case-Exclusions.md`.

## Notes

- Decision: amendment to ADR-0080 decision 2 (an amendment avoids ADR-number collisions between
  lanes): play the case with an echo-and-close seam, not an exclusion. Work filed as BL-1050.
- Measurement: relied on BL-396's recorded run (Surl's server times out at 60 s); Record-CurlExchange.ps1 has no telnet mode, so no new measurement was made.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0080 amended: TELNET server half plays test 1452 via an echo-and-close seam; build filed as BL-1050
