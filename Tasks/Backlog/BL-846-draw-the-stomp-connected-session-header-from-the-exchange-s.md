---
id: BL-846
title: Draw the STOMP CONNECTED session header from the exchange's seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-839]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests]
requirement: none
created: 2026-11-05
completed:
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

- [ ] `StompServerConnectionTests.Connected_SameSeededIdentifiers_SendsTheSameSessionHeader` passes, and different
      seeds send different headers of the same length.
- [ ] The library stays at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-11-05: Created.
