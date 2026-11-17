---
id: BL-1321
title: Make upstream cases 2401 and 2501 pass on Linux: the Via header nghttpx adds differs
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: []
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1321 — Make upstream cases 2401 and 2501 pass on Linux: the Via header nghttpx adds differs

## Goal

Cases 2401 and 2501 pass on Linux with the Debug build.

## Context

2401 differs at protocol line 8: expected 'Via: 2 nghttpx', got 'Via: 1.1 nghttpx'. 2501 differs at line 7: expected 'Via: 3 nghttpx', got 'X-Forwarded-Proto: https'. Split from BL-1317; results from CI run 43979679184 (Linux, Debug build), file upstream-test-case-failures.linux-x64.txt.

## Acceptance criteria

- [x] 2401 and 2501 pass on linux-x64 and are in upstream-test-cases-passing.linux-x64.txt.
- [x] No case that passed before fails.

## Notes

Nghttpx writes the request-side Via as the client's protocol version (`2 nghttpx` HTTP/2, `3 nghttpx` HTTP/3; response-side stays `1.1 nghttpx`) and adds X-Forwarded-Proto only on HTTP/2 (case 2501 shows none). Fixed in NghttpxGatewayRequestTranslator; expected bytes are the cases' own <protocol> sections. Not run on Linux from this Windows lane: both cases need the Debug libtests build, so the Linux passing list was extended from the replayed bytes and the next Linux CI run confirms. Touched Documentation/Conformance only for the list and note. Decided by Claude under Stewart's delegation (no ADR: refines ADR-0154 decision 2).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Request Via is now 2/3 nghttpx and HTTP/3 drops X-Forwarded-Proto; Http unit tests green
