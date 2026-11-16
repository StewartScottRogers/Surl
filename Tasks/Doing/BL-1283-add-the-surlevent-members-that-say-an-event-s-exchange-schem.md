---
id: BL-1283
title: Add the SurlEvent members that say an event's exchange, scheme, account and members, with a guard test (ADR-0171)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1245, BL-1273]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Control.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: FR-060
created: 2026-11-16
completed:
---
# BL-1283 — Add the SurlEvent members that say an event's exchange, scheme, account and members, with a guard test (ADR-0171)

## Goal

SurlEvent gains virtual ExchangeId, Scheme and Account, a KindName derived from the type, and WriteMembers(ISurlEventMemberWriter); every event record overrides what it has.

## Context

ADR-0171 (BL-1245) decides how a SurlEvent kind reaches the three hosts: the event says what hosts read; hosts keep no per-type switch.

## Acceptance criteria

- [ ] `ISurlEventMemberWriter` (Text, Number, Flag, Choice(name, Enum), Bytes(name, ReadOnlyMemory<byte>)) and the `SurlEvent` members exist, AOT-safe, no reflection.
- [ ] `SurlEventContractTests` has a test that fails when an exported SurlEvent subclass writes none of its public properties through WriteMembers, or declares an ExchangeId/Scheme/Account property without overriding the base member.
- [ ] Build clean, fast tests green, 100% line and branch coverage on the touched libraries.

## Notes

- 2026-11-16: A record that overrides SurlEvent.ExchangeId must declare it long?, so ExchangeStartedEvent, ExchangeEndedEvent and ExchangeBytesEvent change from long to long? (CS8866). That breaks ControlEventLogView.cs:21, ControlExchangeWriter.cs:85 (Surl.Control.UnitLibrary, in BL-1273 touches) and ServingEngineTests.Events.cs:106 (Surl.Core.UnitTests); Surl.Api may follow. Design done in the uncommitted work: base virtual get-only ExchangeId/Scheme/Account, KindName snake_case, WriteMembers, ISurlEventMemberWriter; records override with get-only properties from their parameters. Still to do: fix the consumers, add the guard test and coverage.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Needs Surl.Control.UnitLibrary, which BL-1273 (Doing) touches; waits for BL-1273
- 2026-11-16: Backlog -> Doing.
