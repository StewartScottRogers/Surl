---
id: BL-1294
title: Record the Linux RTSP conformance results against ADR-0074
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1289]
touches: [Documentation/Planning/Decisions/ADR-0074-how-the-rtsp-server-answers-upstream-curl.md]
requirement: FR-051
created: 2026-11-16
completed: 2026-11-16
---
# BL-1294 — Record the Linux RTSP conformance results against ADR-0074

## Goal

ADR-0074 states what the `linux-x64` reference build of upstream curl 8.21.0 did against `surl` over `rtsp` in BL-1289's CI run, instead of "Not measured: the Linux build".

## Context

- ADR-0074, Context: "**Not measured:** the Linux build (the lane runs Windows only; it is the same tag's `lib/rtsp.c`, and BL-318 runs every case there through CI)". Decision 11 says the cases run "on Windows and Linux; on macOS each case reports Inconclusive (ADR-0026)", because the `osx-arm64` reference build lists no `rtsp`. The last bullet of the ADR's later section says "If the Linux build or the libcurl measurements disagree with a decision, the finding amends it here." The Roadmap's Milestone 5 is "still open" for this.
- The measurement is in `Documentation/Conformance/Conformance-Off-Windows.md` (BL-1289), the RTSP class(es). Confirm macOS shows them Inconclusive, as ADR-0026 decides.
- BL-1297 also edits ADR-0074; their `touches` overlap, so they never run together. Put this record in its own short amendment.
- Record, do not decide. A failure becomes a filed task (ID in Notes), not a code change here.
- Stand-in: not applicable; a record of upstream curl's results.

## Acceptance criteria

- [x] ADR-0074 has an amendment, "Measured on Linux", naming BL-1289's CI run and the `linux-x64` reference pin's SHA-256, giving the RTSP tests passed, Inconclusive and failed on `linux-x64`.
- [x] The amendment says whether every RTSP test was Inconclusive on `osx-arm64`, as ADR-0026 decides.
- [x] The Context's "Not measured: the Linux build" points to the amendment.
- [x] A task is filed for each failed RTSP test, its ID in Notes, or Notes says none failed.

## Notes

None failed: all 16 RTSP tests passed on linux-x64, all 16 Inconclusive on osx-arm64. No task filed.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ADR-0074 amended with the Linux RTSP results: 16 passed on linux-x64, 16 Inconclusive on osx-arm64, none failed
