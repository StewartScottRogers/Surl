---
id: BL-1122
title: Bring Roadmap.md and Requirements.md up to date with the finished board
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Roadmap.md, Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-14
completed:
---
# BL-1122 — Bring Roadmap.md and Requirements.md up to date with the finished board

## Goal

Every status statement in `Documentation/Planning/Roadmap.md` and in
`Documentation/Product/Requirements.md` is true of the board and the code as of this task's commit.
No "not built", "Blocked" or "waits on BL-…" remains for work that is in `Tasks/Done`.

## Context

The planning pass of 2026-11-14 (BL-1113 to BL-1123) found these statements stale. Check each one
against `Tasks/Done/**` and the code before rewriting it, and state what is true now:

- Roadmap, Milestone 6:
  - "BL-361, Blocked": BL-361 is in `Done/2026-11-12_0707`, and ADR-0151 pinned the three builds;
  - "a clean full run waits on BL-604 and BL-1059": both are Done;
  - "until BL-724 and BL-768 confirm them": both are Done.
- Roadmap, "Track — Shared stores": the "Not built" line names BL-975, BL-507, BL-514, BL-973 and
  BL-492 (mqtt5, amqp, amqp1, kafka, redis), BL-671 to BL-673 (the LDAP account source, ADR-0149),
  BL-669, BL-670 and BL-821. All are Done.
- Roadmap, "Track — The hosts": BL-816 is deferred, but its split BL-1016, BL-1022, BL-972 and
  BL-977 is Done, and BL-821 is Done. Confirm in `Application/Surl.Control.UnitLibrary`,
  `Application/Surl.Console` and `Application/Surl.Api` whether `--control` is
  now served, and say so.
- Roadmap, Milestone 7 and "Track — Extended servers": check how many extended clients
  `Documentation/Conformance/CurlPort/Extended.md` now records against "4 of the 19".
- Requirements.md, the opening paragraph:
  - "FR-058 … it is not built yet" and "FR-060 to FR-065 … none is built yet": restate each from
    the rows and the code;
  - NFR-006's "(BL-361, Blocked …)", and FR-054 and FR-055's "until BL-724 / BL-768 confirms them".
- Leave every statement about the http/2 and http/3 upstream test-case halves as it is. BL-1123
  updates those once BL-1121 has measured them.

This is prose only: no code, no ADR, no task moves.

## Acceptance criteria

- [ ] `Roadmap.md` names no task as Blocked, Deferred or pending that is in `Tasks/Done`.
      `git grep -n -E "BL-(361|604|1059|724|768|975|507|514|973|492|671|672|673|669|670|821)\b" Documentation/Planning/Roadmap.md`
      shows each remaining mention describing finished work.
- [ ] Each roadmap milestone's and track's **Status** line states, with today's date, what is still
      open. Each open item names either a task on the board or "no task filed".
- [ ] `Requirements.md`'s opening paragraph says which of FR-058 and FR-060 to FR-065 are built, and
      its "Last updated" line is today's date.
- [ ] NFR-006, FR-054 and FR-055 no longer cite BL-361, BL-724 or BL-768 as pending.

## Notes

## Log

- 2026-11-14: Created.
