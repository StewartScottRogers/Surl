---
id: BL-1296
title: Record in ADR-0073 the SMB choices its building tasks made where the ADR was silent
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0073-how-the-smb-server-answers-upstream-curl-and-checks-its-ntlmv1-session-setup.md]
requirement: FR-050
created: 2026-11-16
completed:
---
# BL-1296 — Record in ADR-0073 the SMB choices its building tasks made where the ADR was silent

## Goal

Every behaviour choice a Done task made for the SMB server where ADR-0073 said nothing is written in an ADR-0073 amendment, naming the task, so the ADR is the one place that says how `Surl.Protocol.Smb` answers.

## Context

- The Roadmap's Milestone 5 Status: "choices the building tasks made where ADR-0072, ADR-0073 and ADR-0074 were silent are recorded in those tasks, not yet in the ADRs (no task filed)." This is that task for SMB.
- Find the tasks: `git grep -l "Protocol.Smb.UnitLibrary" -- "Tasks/Done/**/BL-*.md"` (14 tasks whose `touches` name it on 2026-11-16). Read each one's `Notes` and `Log` for a choice it made: a status code, a limit, an ordering, a refusal, a default, a field value. A choice already stated in ADR-0073, ADR-0075 or a later ADR is not repeated.
- Confirm each choice against the code as it is now (`Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary`) and name the unit test that pins it; a choice the code no longer makes is left out and said so in Notes.
- BL-1293 also amends ADR-0073 ("Measured on Linux and macOS"); keep this in its own amendment.
- Docs only: no behaviour changes. A choice that looks wrong against MS-CIFS/MS-SMB or upstream curl 8.21.0's `lib/smb.c` behaviour becomes a filed task, ID in Notes.
- Stand-in: not applicable; this records existing behaviour.

## Acceptance criteria

- [ ] ADR-0073 has an amendment, "Choices made while building", marked "Decided by Claude under Stewart's delegation", with one numbered item per choice: what `surl` does, why, the task that made it, and the test that pins it.
- [ ] Notes lists every Done task read and, for each, "choice recorded as item N", "already in an ADR (which)" or "no choice".
- [ ] Every test named in the amendment exists (`git grep -n <test name> -- Protocols Conformance` finds it).

## Notes

## Log

- 2026-11-16: Created.
