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
completed: 2026-11-16
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

- [x] ADR-0073 has an amendment, "Choices made while building", marked "Decided by Claude under Stewart's delegation", with one numbered item per choice: what `surl` does, why, the task that made it, and the test that pins it.
- [x] Notes lists every Done task read and, for each, "choice recorded as item N", "already in an ADR (which)" or "no choice".
- [x] Every test named in the amendment exists (`git grep -n <test name> -- Protocols Conformance` finds it).

## Notes
- Amendment "Choices made while building" added to ADR-0073 (10 items). Tasks read, each result:
  BL-219 no choice; BL-290 items 2 (its `--max-message` 0 choice has no test, so left out and said so in the ADR); BL-296 item 1 (its string choices are already in ADR-0073's 2026-09-30 amendment); BL-297 items 3, 4; BL-298 items 6 to 9; BL-319 no choice; BL-348 item 5; BL-660 item 10 (rest already in ADR-0102); BL-397 no choice (harness); BL-795 already in ADR-0135; BL-909 already in ADR-0135 amendment 1; BL-908 no choice; BL-1080 already in ADR-0128 amendment 1; BL-1252 no choice (no wire change).
- Every test named in the amendment was found with `git grep` in Protocols. Nothing looked wrong against MS-CIFS or curl, so no task was filed.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ADR-0073 records ten SMB choices the building tasks made, each with its task and pinning test
