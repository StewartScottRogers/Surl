---
id: BL-592
title: Write the Phase 7 findings summary and the handoff for the Curl port's board
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-580, BL-581, BL-582, BL-583, BL-584, BL-585, BL-586, BL-587, BL-588, BL-589, BL-590, BL-591, BL-937]
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

- 2026-11-11 (lane 3): File names kept as ADR-0089 decision 7 and 9 chose them: `README.md`,
  `Port-Board-Handoff.md`, `Extended.md`.
- Totals across the 13 family files: 1,497 cases, 1,400 agreements, 95 port-defect cases,
  2 not comparable, 0 Surl defects; **17 distinct findings**, one handoff entry each (14 from
  family files, 3 from `Upstream-Test-Cases.md`).
- Choice: one handoff entry per *finding* (a numbered section in a findings file), not per case.
  A finding is the unit the files define and the port's fix. Two pairs share a root (HTTP/2 #1 with
  HTTP/3 #2; HTTP #1 with SSH-1). Each stays its own entry, cross-referenced, because the criterion asks
  for "one entry per finding". The port's planner may merge them.
- Choice: acceptance criteria are written to be checkable in the port's repository (port tests, its
  own upstream-case runner, `curl -V`), never against Surl's tests, which the port cannot run. Where a
  findings file leaves the call to the port's maintainer (HTTP #2 and #4, SSH-1, LDAP F1), the criterion
  offers "or a port ADR records why". Priorities (a suggestion for the port's planner): High for
  F2 (password sent in a simple bind), SMB disabled and no h2 ALPN; Low for order-only and
  version-choice differences.
- Extended track: none of BL-422 … BL-541 is Done on 2026-11-11, so `Extended.md` holds no finding
  and lists all 19 as not yet done.
- Delivered in the session instead of through `align-and-document`. The work is new prose written from
  the 13 findings files already read here, with no code alignment, so a hand-off would have re-read them all.
- Build: `dotnet build` cannot start on this machine, because the pinned SDK 10.0.401 is missing
  (BL-1035). The change is Markdown only, in `Documentation/` (wildcard `**\*.md` in
  `Documentation.projitems`). No test reads these three files: each Phase 7 test reads only its own
  named family file. So no build or test input changed.
- Tell Stewart: the handoff is ready with 17 findings, for him to file by running the port's
  `/task-plan` on `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`.

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-597 replaced by BL-937, the last of the tasks BL-597 was split into (BL-942 to BL-937); BL-597 is Deferred.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Phase 7 summary (README.md), Extended.md and the 17-entry Port-Board-Handoff.md are written for Stewart to file on the port's board
