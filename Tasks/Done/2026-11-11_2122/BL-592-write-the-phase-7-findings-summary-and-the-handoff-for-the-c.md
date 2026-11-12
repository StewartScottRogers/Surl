---
id: BL-592
title: Write the Phase 7 findings summary and the handoff for the Curl port's board
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-580, BL-581, BL-582, BL-583, BL-584, BL-585, BL-586, BL-587, BL-588, BL-589, BL-590, BL-591, BL-937, BL-1035]
touches: [Documentation/Conformance/CurlPort/README.md, Documentation/Conformance/CurlPort/Port-Board-Handoff.md, Documentation/Conformance/CurlPort/Extended.md]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-592 — Write the Phase 7 findings summary and the handoff for the Curl port's board

## Goal

`Documentation/Conformance/CurlPort/README.md` summarises Phase 7: which port commit was measured, against
which upstream builds and Surl commit, and the count of agreements and port findings per protocol family.
`Documentation/Conformance/CurlPort/Port-Board-Handoff.md` holds one entry per port finding, written in the
port's own task shape, so the port's planner can file each one without reading Surl's tests. The file names
are planning names; use the ones BL-576's ADR decision 7 chose.

## Context

- Rules: BL-576's ADR, decisions 6, 7 and 9. ADR-0003 decision 6 says every disagreement is filed on the
  port's own board. **This task does not write to `Z:\repos\Curl`.** Stewart files the handoff himself by
  running the port's `/task-plan` on it (his decision, 2026-11-04; the ADR's decision 7). When this task is
  done, tell him the handoff is ready and how many findings it holds.
- The port's task shape is in its `.claude/skills/task-board/SKILL.md` (read with
  `git -C Z:\repos\Curl show origin/master:.claude/skills/task-board/SKILL.md`). Each entry has a title,
  goal, context and checkable acceptance criteria. It also carries the evidence: the case, the command line,
  the upstream build's SHA-256, the port commit and SHA-256, Surl's commit, both results, and the
  specification section or upstream documentation that decides (https://curl.se/docs/manpage.html,
  https://curl.se/libcurl/c/libcurl-errors.html, curl 8.21.0). The port's `touches` and IDs are left for its
  planner to choose.
- Sources: the per-family findings files written by BL-580 to BL-590, `Upstream-Test-Cases.md` (BL-591) and `Libcurl.md` (BL-936 and BL-937, split from BL-597). ADR-0089 decision 9 puts the extended clients' findings in `Extended.md`, which this task writes.
- Extended clients (ADR decision 9): include each port defect that the extended track's integration tasks
  recorded in their Notes (BL-422, BL-428, BL-434, BL-440, BL-446, BL-451, BL-456, BL-462, BL-469, BL-477,
  BL-486, BL-493, BL-502, BL-508, BL-515, BL-523, BL-529, BL-534, BL-541) that are `Done` when this task
  runs. Mark them "judged by the specification" and name each task not yet done. This task does not wait
  for them. A later run of this task, filed as a new task, adds their findings.

## Acceptance criteria

- [x] `README.md` names the measured port commit, upstream builds and Surl commit, and has a table with one
      row per family file: family, cases, agreements, port findings, not comparable.
- [x] `Port-Board-Handoff.md` has one entry per finding in the family files and `Upstream-Test-Cases.md`,
      and no others. Each entry has a title, goal, context with the evidence fields above, and acceptance
      criteria in the port's task shape.
- [x] `Port-Board-Handoff.md` opens by saying how it is filed on the port's board, as the ADR's decision 7
      records, and that Surl does not change to make the port pass (ADR-0003 decision 6).
- [x] Extended-track findings are present, marked as above, and the extended tasks not yet done are listed.

## Notes

- 2026-11-11 (lane 2): The docs are written and complete on branch factory/BL-592-lane-3-20261111-060243
  (commit a323bbb4: README.md, Port-Board-Handoff.md with 17 findings, Extended.md). Integration needs
  `dotnet build`, which cannot start: global.json pins SDK 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2
  and 11.0.100 are installed. Added BL-1035 to depends-on. Next run: `git cherry-pick --no-commit a323bbb4`,
  move the task file back from Done to Doing (the commit moves it), build, test, move to Done.
- 2026-11-11 (lane 4): Took the three docs from a323bbb4 (not its task-file move). SDK 10.0.404 is now
  installed and `global.json`'s `latestFeature` roll-forward accepts it, so `dotnet build` is clean and the
  fast tests pass. Lane 3's choices stand: one handoff entry per finding; criteria checkable in the port's
  own repository; file names as ADR-0089 decisions 7 and 9 chose.
- Extended track brought up to date: BL-456 (`socketio`), BL-462 (`webtransport`), BL-477 (`coap`) and
  BL-541 (`xmpp`) are now Done. Only BL-462's Notes record a port defect (the client exits 0 when the
  CONNECT is refused 401); it is `Extended.md` EXT-1 and handoff entry 18, judged by
  draft-ietf-webtrans-http3-16 section 3.2. BL-477's "exits 0 on a CoAP error response" is not counted:
  BL-477 itself recorded no port defect. Extended cases: 21 (4 + 4 + 7 + 6), 20 agreements, 1 defect.
  Totals now 1,518 cases, 1,420 agreements, 96 port-defect cases, 18 findings, 2 not comparable.
  15 extended tasks remain not done and are listed in `Extended.md`.
- Tell Stewart: the handoff is ready with 18 findings, for him to file by running the port's
  `/task-plan` on `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`.

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-597 replaced by BL-937, the last of the tasks BL-597 was split into (BL-942 to BL-937); BL-597 is Deferred.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 3 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-592-lane-3-20261111-060243; start with git cherry-pick --no-commit factory/BL-592-lane-3-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot start; docs are done on factory/BL-592-lane-3-20261111-060243
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Phase 7 summary (README.md), Extended.md with EXT-1 and the 18-entry Port-Board-Handoff.md are ready for Stewart to file on the port's board
