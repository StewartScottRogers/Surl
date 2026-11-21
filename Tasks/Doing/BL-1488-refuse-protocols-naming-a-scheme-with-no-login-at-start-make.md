---
id: BL-1488
title: Refuse protocols= naming a scheme with no login at start (MakesLogin on registrations)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1483]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1488 — Refuse protocols= naming a scheme with no login at start (MakesLogin on registrations)

## Goal

`protocols=` naming a scheme whose server makes no login (`gopher`, `tftp`, `dict`, `telnet`) is refused at start with exit 2 and `scheme <scheme> has no login`.

## Context

ADR-0102 section 4.3 amendment of 2026-11-20 (BL-1483) is the specification. Registrations are in
`Application/Surl.Composition.UnitLibrary/ProtocolServerRegistrations.cs`; the parse-time check is `ConfiguredMailboxReader.cs` `CheckScheme`.

## Acceptance criteria

- [ ] A registration states whether its server makes a login (`MakesLogin`), true for those built from `AuthenticationPolicyFor`.
- [ ] Start refuses `protocols=gopher` (and each login-less scheme) with exit 2 and `scheme gopher has no login`, before any `Listening on` line.
- [ ] `protocols=imap,ftp` still starts; an unregistered scheme still gives `unknown scheme <scheme>`.
- [ ] Unit tests cover each branch; build clean, fast tests green.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
