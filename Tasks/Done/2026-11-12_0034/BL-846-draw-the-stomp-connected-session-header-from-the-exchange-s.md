---
id: BL-846
title: Draw the STOMP CONNECTED session header from the exchange's seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-846 — Draw the STOMP CONNECTED session header from the exchange's seedable identifier source

## Goal

The `session` header of a STOMP `CONNECTED` frame comes from `ExchangeContext.Identifiers`, so a seeded
instance sends the same session ID every run.

## Context

- Decision: ADR-0124 decision 6 - generated identifiers that grant nothing are seedable. STOMP 1.2's `session`
  header is informational ("a session identifier that uniquely identifies the session"); no later frame presents
  it, so it admits nothing. Found by BL-786's audit.
- `Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary/StompServerConnection.cs:130` -
  `RandomNumberGenerator.GetHexString(SessionHexDigits, lowercase: true)`.
- Fix: draw the same number of hex digits from `context.Identifiers`. BL-532 (in Doing on 2026-11-05) also
  touches this library; the board runs them one after the other.

## Acceptance criteria

- [x] `StompServerConnectionTests.Connected_SameSeededIdentifiers_SendsTheSameSessionHeader` passes, and different
      seeds send different headers of the same length.
- [x] The library stays at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

- 2026-11-11 (lane 4): Change made and left in the lane's working tree (the shift stashes it):
  `StompServerConnection.Connected` fills 16 bytes from `context.Identifiers` and writes them with
  `Convert.ToHexStringLower` - still 32 lowercase hex digits, so the existing `session:[0-9a-f]{32}`
  masks hold. The test project gains `SeededTestIdentifierRandom` (a `System.Random(seed)` source,
  standing in for `--random-seed`; the test project does not reference `Surl.Core`, so its
  `SeededIdentifierRandom` was not used) and the test
  `Connected_SameSeededIdentifiers_SendsTheSameSessionHeader` (seeds 7, 7, 8). The library's
  CLAUDE.md says where the session comes from.
- Verified with the installed SDK 11.0.100 from outside the repository (so `global.json` was not
  read): the Stomp library and tests build clean and all 176 tests pass. The new code has no
  branches, so coverage stays at 100%.
- 2026-11-11 (lane 6): SDK 10.0.404 now satisfies global.json; change redone, build clean, 176 Stomp tests pass.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; change verified with SDK 11 (176 Stomp tests pass) and left in the working tree
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. STOMP CONNECTED session header drawn from ExchangeContext.Identifiers; seeded runs repeat it
