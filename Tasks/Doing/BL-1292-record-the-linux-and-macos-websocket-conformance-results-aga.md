---
id: BL-1292
title: Record the Linux and macOS WebSocket conformance results against ADR-0071
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1289]
touches: [Documentation/Planning/Decisions/ADR-0071-how-the-websocket-server-answers-upstream-curl.md]
requirement: FR-048
created: 2026-11-16
completed:
---
# BL-1292 — Record the Linux and macOS WebSocket conformance results against ADR-0071

## Goal

ADR-0071 states what the Linux and macOS reference builds of upstream curl 8.21.0 did against `surl` over `ws` and `wss` in BL-1289's CI run, instead of "Not measured: the Linux and macOS static builds".

## Context

- ADR-0071, Context, "**Not measured:** the Linux and macOS static builds (the lane runs Windows only); they are the same tag's `lib/ws.c`, and BL-304 runs its cases on all three through CI." And before "Alternatives considered": "where the Linux or macOS build gives another exit, it is pinned per platform in its own test ... and recorded against this ADR." The Roadmap's Milestone 4 is "not closed" for that reason.
- The libcurl rows of Amendment 1 cannot run off Windows (ADR-0071 decision 10: the static builds carry no shared library). That stays as it is; record only that those tests were Inconclusive, if the `.trx` shows them so.
- The measurement is in `Documentation/Conformance/Conformance-Off-Windows.md` (BL-1289), class(es) whose name contains `Ws` or `WebSocket`.
- Record, do not decide. A failure becomes a filed task (ID in Notes), not a code change here.
- Stand-in: not applicable; a record of upstream curl's results.

## Acceptance criteria

- [ ] ADR-0071's "Not measured" sentence is replaced or followed by a statement naming BL-1289's CI run and giving, for `linux-x64` and `osx-arm64`, the WebSocket tool tests passed, Inconclusive and failed.
- [ ] Each WebSocket test Inconclusive or failed off Windows is named in ADR-0071 with its reason.
- [ ] The libcurl rows' off-Windows status in ADR-0071 still matches decision 10.
- [ ] A task is filed for each failed WebSocket test, its ID in Notes, or Notes says none failed.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
