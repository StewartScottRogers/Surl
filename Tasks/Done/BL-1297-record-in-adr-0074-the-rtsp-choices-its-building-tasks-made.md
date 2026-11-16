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
completed: 2026-11-16
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

- [x] ADR-0074 has an amendment, "Choices made while building", marked "Decided by Claude under Stewart's delegation", with one numbered item per choice: what `surl` does, why, the task that made it, and the test that pins it.
- [x] Notes lists every Done task read and, for each, "choice recorded as item N", "already in an ADR (which)" or "no choice".
- [x] Every test named in the amendment exists (`git grep -n <test name> -- Protocols Conformance` finds it).

## Notes

Docs only; ADR-0074 gained "Amendment - Choices made while building" (items 1 to 8), unnumbered so it cannot collide with BL-1294's amendment. Tasks read (15 reference the library; BL-286 is the ADR itself):
- BL-286: no choice (it wrote the ADR). BL-219, BL-319, BL-396: no choice (reference lists, docs, RTSP libtests skipped).
- BL-313: choice recorded as items 1 to 4 (its interim 501s for later methods are obsolete and left out).
- BL-314: choice recorded as items 5 to 7.
- BL-315: already in ADR-0074 (decision 5, folded by BL-338); its interim 501 for mode=record is obsolete.
- BL-316: already in ADR-0074 (decision 6, folded by BL-339); its HttpMessage `PeekByteAsync` is not an RTSP answer.
- BL-337: already in ADR-0074 (Amendment 1, decision 5 amended). BL-338, BL-339: no choice (the folding tasks).
- BL-661: choice recorded as item 8.
- BL-793: already in an ADR (ADR-0134). BL-902: already in an ADR (ADR-0141 amendment 1).
- BL-1256: no choice (no wire change).
All 12 tests named were found by grep in Protocols/Http/Surl.Protocol.Rtsp.UnitTests. No choice looked wrong against RFC 2326 or lib/rtsp.c, so no task filed.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ADR-0074 now records eight RTSP choices its building tasks made, each with task and pinning test
