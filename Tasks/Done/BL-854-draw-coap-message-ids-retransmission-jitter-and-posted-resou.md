---
id: BL-854
title: Draw CoAP message IDs, retransmission jitter and posted resource names from the seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-854 — Draw CoAP message IDs, retransmission jitter and posted resource names from the seedable identifier source

## Goal

A CoAP flow's first message ID, its retransmission jitter and the names of resources created by POST into a
directory come from `ExchangeContext.Identifiers`, so a seeded instance repeats them every run.

## Context

- Decision: ADR-0124 decision 6 - message IDs and generated resource names are seedable. Found by BL-786's audit.
- `Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary/CryptographicCoapRandomSource.cs` (static `Instance`,
  stateless) feeds `ICoapRandomSource.DrawMessageId` (`CoapFlowSession.cs:59`), `DrawFraction`
  (`CoapFlowSession.cs:299`, RFC 7252 section 4.8 ACK_RANDOM_FACTOR) and `Fill` (`CoapResourceAnswerer.cs:293`,
  POST names). None is secret: CoAP tokens are the client's, and the DTLS randoms of the secure server stay in
  `Surl.Dtls` on `RandomNumberGenerator`.
- Fix: an `ICoapRandomSource` built over the exchange's `IIdentifierRandom` (message ID from 2 bytes, fraction
  from 4 bytes as today's steps), used by the flow and the answerer instead of `CryptographicCoapRandomSource`;
  keep or drop that class as the tests need, but keep `ICoapRandomSource` as the test seam.

## Acceptance criteria

- [x] `CoapFlowSessionTests.FirstMessageIdAndJitter_SameSeededIdentifiers_AreTheSameEveryRun` passes: two flows given
      seeded sources with one seed and exchange ID send the same first message ID and draw the same initial
      retransmission timeout.
- [x] `CoapResourceAnswererTests.PostIntoDirectory_SameSeededIdentifiers_CreatesTheSameName` passes.
- [x] The library stays at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

- 2026-11-11 (lane 6): Implemented, not yet built - `dotnet build` cannot resolve the SDK `global.json` pins
  (10.0.401; the machine has 10.0.100-rc.1 and 11.x), so the task waits on BL-1035. The code is left uncommitted
  for the shift to stash. Design: `IdentifierCoapRandomSource` over `IIdentifierRandom` (Message ID = next 2 bytes
  big-endian; fraction = low 30 bits of next 4 bytes over 2^30; draws serialised under a lock because a flow's
  separate responses, observers and requests may draw at once) replaces `CryptographicCoapRandomSource`. Both
  servers keep an internal `ICoapRandomSource?` seam; null (the public constructors) makes `CoapFlowServing`
  build the source from `context.Identifiers` and the `CoapResourceAnswerer` per flow. Tests reference
  `Surl.Core` for `SeededIdentifierRandom` (as Ftp's tests do). New: `IdentifierCoapRandomSourceTests`,
  `RecordingCoapRandomSource`, and the two named tests.

- 2026-11-11 (lane 3): Redone from scratch (lane 6's uncommitted work was not in this worktree), SDK now
  builds. Servers keep an internal nullable `ICoapRandomSource` seam; null builds `IdentifierCoapRandomSource`
  (2 bytes big-endian Message ID; low 30 bits of 4 bytes / 2^30 fraction; draws under a lock) per flow from
  `context.Identifiers`, and the answerer per flow. `CryptographicCoapRandomSource` stays only for the
  context-free connection-limit refusal's Message ID. Tests reference `Surl.Core` for `SeededIdentifierRandom`.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so the CoAP change cannot be built or tested
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. CoAP message IDs, retransmission jitter and posted names draw from the exchange's seedable identifier source
