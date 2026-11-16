---
id: BL-1283
title: Add the SurlEvent members that say an event's exchange, scheme, account and members, with a guard test (ADR-0171)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1245]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
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

## Log

- 2026-11-16: Created.
