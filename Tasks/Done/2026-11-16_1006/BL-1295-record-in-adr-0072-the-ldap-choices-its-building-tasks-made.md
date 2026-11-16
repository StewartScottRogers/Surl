---
id: BL-1295
title: Record in ADR-0072 the LDAP choices its building tasks made where the ADR was silent
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md]
requirement: FR-049
created: 2026-11-16
completed: 2026-11-16
---
# BL-1295 — Record in ADR-0072 the LDAP choices its building tasks made where the ADR was silent

## Goal

Every behaviour choice a Done task made for the LDAP server where ADR-0072 said nothing is written in an ADR-0072 amendment, naming the task, so the ADR is the one place that says how `Surl.Protocol.Ldap` answers.

## Context

- The Roadmap's Milestone 5 Status: "choices the building tasks made where ADR-0072, ADR-0073 and ADR-0074 were silent are recorded in those tasks, not yet in the ADRs (no task filed)." This is that task for LDAP.
- Find the tasks: `git grep -l "Protocol.Ldap.UnitLibrary" -- "Tasks/Done/**/BL-*.md"` (15 tasks whose `touches` name it on 2026-11-16: BL-219, BL-289, BL-306 to BL-309, BL-319, BL-349, BL-797, BL-847, BL-908, BL-920, BL-1162, BL-1163, BL-1219; also read BL-327 and BL-342, which built Kerberos inside its binds). Read each one's `Notes` and `Log` for a choice it made: a limit, an answer code, an ordering, a refusal, a default, a format. A choice already stated in ADR-0072 (with its amendments), ADR-0076, ADR-0078 or ADR-0149 is not repeated.
- Confirm each choice against the code as it is now (`Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary`) and name the unit test that pins it; a choice the code no longer makes is left out and said so in Notes.
- Docs only: no behaviour changes. A choice that looks wrong against RFC 4511 or upstream curl 8.21.0's behaviour becomes a filed task, ID in Notes.
- Stand-in: not applicable; this records existing behaviour.

## Acceptance criteria

- [x] ADR-0072 has an amendment, "Choices made while building", marked "Decided by Claude under Stewart's delegation", with one numbered item per choice: what `surl` does, why, the task that made it, and the test that pins it.
- [x] Notes lists every Done task read and, for each, "choice recorded as item N", "already in an ADR (which)" or "no choice".
- [x] Every test named in the amendment exists (`git grep -n <test name> -- Protocols Conformance` finds it).

## Notes

- Added "Amendment 2 - Choices made while building" to ADR-0072: 15 numbered items, each with its task and pinning test. Every named test found with `git grep`.
- Tasks read: BL-219 no choice; BL-289 items 1-3 (framing limits already in decision 7); BL-306 items 4-11 (base invalidDNSyntax already in decision 3); BL-307 item 13; BL-308 already in an ADR (ADR-0072, BL-335); BL-309 already in an ADR (ADR-0072, BL-341); BL-319 no choice; BL-327 already in an ADR (ADR-0072 Amendment 1); BL-342 already in an ADR (ADR-0078); BL-349 item 14; BL-797 already in an ADR (ADR-0139); BL-847 item 12; BL-908 no choice; BL-920 already in an ADR (ADR-0141); BL-1162 no choice; BL-1163 already in an ADR (ADR-0158); BL-1219 item 15.
- No choice looked wrong against RFC 4511 or curl, so no follow-up task was filed. BL-1219 documents `userPassword` omission in read-back; behaviour unchanged.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ADR-0072 Amendment 2 records the LDAP choices made while building, each with its task and test
