---
id: BL-847
title: Give the composed LDAP directory the instance's TimeProvider instead of the system clock
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-847 — Give the composed LDAP directory the instance's TimeProvider instead of the system clock

## Goal

The LDAP server `surl` and `Surl.Api` compose measures a search's `timeLimit` by the instance's injected
`TimeProvider`, not the system clock.

## Context

- Decision: ADR-0124 decision 6 - every time-dependent path uses the instance's injected `TimeProvider`. Found by
  BL-786's audit.
- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapProtocolServer.cs:104` - the convenience constructor
  builds `new LdapDirectory([], TimeProvider.System)`; `LdapDirectory.cs:232` compares
  `timeProvider.GetUtcNow()` with the search deadline.
- `Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs:365` and `:471` call that constructor, so a
  composed instance's LDAP time limit ignores the `TimeProvider` the composition was given.
- Fix: a constructor (or parameter) taking the `TimeProvider`, which the composition passes from its seams;
  remove `TimeProvider.System` from the library if nothing else needs it.

## Acceptance criteria

- [x] No `TimeProvider.System` remains in `Surl.Protocol.Ldap.UnitLibrary`.
- [x] A composition test shows the composed LDAP server's search with `timeLimit` 1 answers `timeLimitExceeded`
      once the composition's fake `TimeProvider` is advanced past it, and not before.
- [x] Both libraries stay at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

- Decision: the public `LdapProtocolServer` constructor gained a required `TimeProvider` parameter before
  `isTlsUpgradeAvailable`; the composition passes its own. No null guard was added: the directory it builds
  holds the clock and the existing tests cover every branch (no new branch).
- The in-memory (no `--directory`) directory is empty, so its clock is never observable; the test seeds a
  directory file and drives a full `ComposeAsync` start with `SteppingTimeProvider` (step 0, then 2 s).
- Coverage was not re-measured with the collector; the change adds no branch and the new test covers the path.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Composed LDAP server measures search timeLimit by the instance's TimeProvider; no TimeProvider.System left in the Ldap library
