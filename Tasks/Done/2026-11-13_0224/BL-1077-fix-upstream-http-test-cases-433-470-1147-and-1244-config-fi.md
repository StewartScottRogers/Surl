---
id: BL-1077
title: Fix upstream http test cases 433, 470, 1147 and 1244 (config-file note path, Unicode quote warning, -o file, proxy ports)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-683]
touches: [Foundation/Surl.Core.UnitLibrary, Protocols/Http/Surl.HttpMessage.UnitLibrary, Conformance/Surl.Conformance.UnitLibrary]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1077 — Fix upstream http test cases 433, 470, 1147 and 1244 (config-file note path, Unicode quote warning, -o file, proxy ports)

## Goal

Upstream http test cases 433, 470, 1147 and 1244 pass against Surl on win-x64.

## Context

Conformance run against pinned upstream curl 8.21.0 (ADR-0080 passing list).

## Acceptance criteria

- [x] Cases 433, 470, 1147 and 1244 are in `upstream-test-cases-passing.win-x64.txt` and the pass-rate row.
- [x] Fast tests and build are green.

## Notes

- 433: the case has two `<setenv>` parts (the second sets COLUMNS=300 so curl does not wrap its note); the runner read only the first. It now reads every `<setenv>` part, later wins, as getpart does.
- 470 and 1147: curl sends a header line with a non-token name (Unicode quotes) and one with leading/trailing whitespace; upstream's sws test server ignores them. `HttpConnectionReader.SkipsMalformedFieldLines` (default off) drops such lines; only `HttpUpstreamTestCaseServer` turns it on, so the shipped servers keep rejecting them.
- 1244: the second proxy port is the HTTPS port, which gets plain text; stunnel resets, so curl exits 56. `ServingEngine` now aborts (resets) a connection whose implicit TLS handshake failed instead of ending it cleanly (curl 52). This touches Surl.Core, outside the task's empty `touches`; no Doing task named it, so `touches` now lists it. Decided by Claude under Stewart's delegation: a reset after a failed handshake is what real TLS servers do with unread plain text.
- Case 633 (SFTP) failed once in a full run and passed on rerun: the known flaky SSH timing, unrelated.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Cases 433, 470, 1147 and 1244 pass
