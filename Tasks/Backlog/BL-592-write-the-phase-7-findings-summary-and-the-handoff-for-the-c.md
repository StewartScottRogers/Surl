---
id: BL-592
title: Write the Phase 7 findings summary and the handoff for the Curl port's board
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-580, BL-581, BL-582, BL-583, BL-584, BL-585, BL-586, BL-587, BL-588, BL-589, BL-590, BL-591]
touches: [Documentation/Conformance/CurlPort/README.md, Documentation/Conformance/CurlPort/Port-Board-Handoff.md]
requirement: none
created: 2026-11-04
completed:
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
  port's own board. **This task does not write to `Z:\repos\Curl`.** Filing from the handoff happens as the
  ADR's decision 7 records: either Stewart asks the port's `/task-plan` to file it, or the port's planner
  reads it.
- The port's task shape is in its `.claude/skills/task-board/SKILL.md` (read with
  `git -C Z:\repos\Curl show origin/master:.claude/skills/task-board/SKILL.md`). Each entry has a title,
  goal, context and checkable acceptance criteria. It also carries the evidence: the case, the command line,
  the upstream build's SHA-256, the port commit and SHA-256, Surl's commit, both results, and the
  specification section or upstream documentation that decides (https://curl.se/docs/manpage.html,
  https://curl.se/libcurl/c/libcurl-errors.html, curl 8.21.0). The port's `touches` and IDs are left for its
  planner to choose.
- Sources: the per-family findings files written by BL-580 to BL-590 and `Upstream-Test-Cases.md` (BL-591).
- Extended clients (ADR decision 9): include each port defect that the extended track's integration tasks
  recorded in their Notes (BL-422, BL-428, BL-434, BL-440, BL-446, BL-451, BL-456, BL-462, BL-469, BL-477,
  BL-486, BL-493, BL-502, BL-508, BL-515, BL-523, BL-529, BL-534, BL-541) that are `Done` when this task
  runs. Mark them "judged by the specification" and name each task not yet done. This task does not wait
  for them. A later run of this task, filed as a new task, adds their findings.

## Acceptance criteria

- [ ] `README.md` names the measured port commit, upstream builds and Surl commit, and has a table with one
      row per family file: family, cases, agreements, port findings, not comparable.
- [ ] `Port-Board-Handoff.md` has one entry per finding in the family files and `Upstream-Test-Cases.md`,
      and no others. Each entry has a title, goal, context with the evidence fields above, and acceptance
      criteria in the port's task shape.
- [ ] `Port-Board-Handoff.md` opens by saying how it is filed on the port's board, as the ADR's decision 7
      records, and that Surl does not change to make the port pass (ADR-0003 decision 6).
- [ ] Extended-track findings are present, marked as above, and the extended tasks not yet done are listed.

## Notes

## Log

- 2026-11-04: Created.
