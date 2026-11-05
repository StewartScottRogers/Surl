---
id: BL-845
title: Draw MQTT 5 assigned client identifiers from the exchange's seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-845 — Draw MQTT 5 assigned client identifiers from the exchange's seedable identifier source

## Goal

The `Assigned Client Identifier` an MQTT 5 CONNACK gives a client that connected with an empty client
identifier comes from `ExchangeContext.Identifiers`, so a seeded instance assigns the same identifier every run.

## Context

- Decision: ADR-0124 decision 6 - generated identifiers that grant nothing are seedable. An MQTT client
  identifier grants nothing by itself: logins are judged by the authentication policy. Found by BL-786's audit.
- `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/Mqtt5BrokerState.cs:213` -
  `internal static string NewAssignedClientIdentifier()` from `RandomNumberGenerator.GetBytes(16)`, drawn again
  until it names no session (line 17's documentation).
- Fix: take the `IIdentifierRandom` as a parameter (the method may stay static: it holds no state); keep the
  `surl-` prefix, the 32 hex digits and the redraw-on-collision rule.

## Acceptance criteria

- [ ] `Mqtt5BrokerStateTests.AssignedClientIdentifier_SameSeededIdentifiers_IsTheSameEveryRun` passes.
- [ ] `Mqtt5BrokerStateTests.AssignedClientIdentifier_CollidesWithASession_IsDrawnAgain` still passes with a seeded
      source whose first draw collides.
- [ ] The library stays at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-11-05: Created.
