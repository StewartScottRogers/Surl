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
completed:
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

- [ ] No `TimeProvider.System` remains in `Surl.Protocol.Ldap.UnitLibrary`.
- [ ] A composition test shows the composed LDAP server's search with `timeLimit` 1 answers `timeLimitExceeded`
      once the composition's fake `TimeProvider` is advanced past it, and not before.
- [ ] Both libraries stay at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-11-05: Created.
