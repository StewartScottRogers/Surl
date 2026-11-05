---
id: BL-854
title: Draw CoAP message IDs, retransmission jitter and posted resource names from the seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-05
completed:
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

- [ ] `CoapFlowSessionTests.FirstMessageIdAndJitter_SameSeededIdentifiers_AreTheSameEveryRun` passes: two flows given
      seeded sources with one seed and exchange ID send the same first message ID and draw the same initial
      retransmission timeout.
- [ ] `CoapResourceAnswererTests.PostIntoDirectory_SameSeededIdentifiers_CreatesTheSameName` passes.
- [ ] The library stays at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-11-05: Created.
