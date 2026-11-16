---
id: BL-1302
title: Bring the Roadmap's Milestones 2 to 5 and the product overview up to date with the off-Windows records and SASL EXTERNAL
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1288, BL-1290, BL-1291, BL-1292, BL-1293, BL-1294, BL-1295, BL-1296, BL-1297, BL-1298, BL-1299, BL-1300]
touches: [Documentation/Planning/Roadmap.md, Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-16
completed:
---
# BL-1302 — Bring the Roadmap's Milestones 2 to 5 and the product overview up to date with the off-Windows records and SASL EXTERNAL

## Goal

The Roadmap's Milestones 2 to 5, the product overview's "Built for Phase 2" to "Built for Phase 5" sections and FR-039, FR-046, FR-048 to FR-051 in `Requirements.md` say what BL-1289 to BL-1300 recorded and proved, and each milestone is marked closed where nothing is left open.

## Context

- Milestones 2 to 5 of `Documentation/Planning/Roadmap.md` were "not closed" on 2026-11-16 for: SSH off Windows pinned as predicted (now ADR-0051 and ADR-0054, BL-1290); mail off Windows not recorded (ADR-0053, ADR-0055, ADR-0056, BL-1291) and SASL `EXTERNAL` proven by unit tests only (BL-1298 to BL-1300, ADR-0049); WebSocket off Windows not recorded (ADR-0071, BL-1292); SMB off Windows and RTSP on Linux not recorded (ADR-0073, ADR-0074, BL-1293, BL-1294); and choices made outside ADR-0072 to ADR-0074 (BL-1295 to BL-1297).
- The product overview repeats them: "Built for Phase 3" ("no result from them is recorded against these ADRs yet. `EXTERNAL` is proven by unit tests only."), and the Phase 2, 4 and 5 sections' "what pinned upstream curl has proven". `Requirements.md`'s intro says "FR-049 to FR-052 each name what pinned upstream curl has not yet proven".
- Read each Done task's Notes first: a follow-up a record task filed for a failure keeps its milestone open, named with its ID.
- What stays open and is not this task's: Milestone 4's libcurl rows cannot run off Windows (ADR-0071 decision 10), which is a decided limit, not an open item; Milestone 6's release tag and Milestone 7's off-Windows measurement (BL-1301) stay as BL-1288 left them.
- Stand-in: not applicable; documentation of what was proved.

## Acceptance criteria

- [ ] Each of Milestones 2 to 5 in `Roadmap.md` either says "Closed (<date>)" with the tasks that closed it, or names each item still open with the task that holds it; none says "predicted", "not yet measured" or "not yet recorded" for anything BL-1290 to BL-1294 recorded.
- [ ] The product overview's "Built for Phase 3" no longer says `EXTERNAL` is proven by unit tests only, and names the `SaslExternal` conformance tests.
- [ ] The product overview's Phase 2 to 5 sections say the Linux and macOS results are recorded in the ADRs, naming `Documentation/Conformance/Conformance-Off-Windows.md`.
- [ ] FR-039, FR-046 and FR-048 to FR-051 in `Requirements.md`, and its intro's sentence about FR-049 to FR-052, are true of the ADRs as amended.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
