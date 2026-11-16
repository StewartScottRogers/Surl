---
id: BL-1297
title: Record in ADR-0074 the RTSP choices its building tasks made where the ADR was silent
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0074-how-the-rtsp-server-answers-upstream-curl.md]
requirement: FR-051
created: 2026-11-16
completed:
---
# BL-1297 — Record in ADR-0074 the RTSP choices its building tasks made where the ADR was silent

## Goal

Every behaviour choice a Done task made for the RTSP server where ADR-0074 said nothing is written in an ADR-0074 amendment, naming the task, so the ADR is the one place that says how `Surl.Protocol.Rtsp` answers.

## Context

- The Roadmap's Milestone 5 Status: "choices the building tasks made where ADR-0072, ADR-0073 and ADR-0074 were silent are recorded in those tasks, not yet in the ADRs (no task filed)." This is that task for RTSP.
- Find the tasks: `git grep -l "Protocol.Rtsp.UnitLibrary" -- "Tasks/Done/**/BL-*.md"` (13 on 2026-11-16: BL-286, BL-313 to BL-316, BL-337, BL-661, BL-793, BL-902, BL-1256 and others). Read each one's `Notes` and `Log` for a choice it made: a status code, a header, a session rule, a limit, a refusal, a default. A choice already stated in ADR-0074 (with Amendment 1) or a later ADR (ADR-0141's command-stage faults, ADR-0168's exchange behind a write) is not repeated.
- Confirm each choice against the code as it is now (`Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary`) and name the unit test that pins it; a choice the code no longer makes is left out and said so in Notes.
- BL-1294 also amends ADR-0074 ("Measured on Linux"); keep this in its own amendment.
- Docs only: no behaviour changes. A choice that looks wrong against RFC 2326 or upstream curl 8.21.0's `lib/rtsp.c` behaviour becomes a filed task, ID in Notes.
- Stand-in: not applicable; this records existing behaviour.

## Acceptance criteria

- [ ] ADR-0074 has an amendment, "Choices made while building", marked "Decided by Claude under Stewart's delegation", with one numbered item per choice: what `surl` does, why, the task that made it, and the test that pins it.
- [ ] Notes lists every Done task read and, for each, "choice recorded as item N", "already in an ADR (which)" or "no choice".
- [ ] Every test named in the amendment exists (`git grep -n <test name> -- Protocols Conformance` finds it).

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
