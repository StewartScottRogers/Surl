---
id: BL-1361
title: Cover the XMPP stanza fault published without a to address
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1361 — Cover the XMPP stanza fault published without a to address

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Xmpp.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.82% branch, 1 failing member.
- `XmppStanzaFaults.FindAsync` (async) - `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary/XmppStanzaFaults.cs:61`,
  95% branch; line 85 is 1 of 2: the applied `error` rule's `FaultAppliedEvent` target `to ?? stanza` is only ever
  published with a `to` (`XmppStanzaRouterTests` line 637 asserts `romeo@im.example.com/orchard`); never with the stanza
  name for a stanza without a `to` attribute.

## Acceptance criteria

- [x] A named `Surl.Extended.Protocol.Xmpp.UnitTests` test applies an `error` fault rule to a stanza with no `to`
      attribute and asserts the single `FaultAppliedEvent` names the stanza (for example `iq`) as its target, and the
      error stanza sent.
- [x] `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Xmpp.UnitLibrary` reports `100 | 100` with 0 failing
      members, and the fast tests are green.

## Notes

- Tests only (test-writer).

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. XMPP error rule on a stanza without a to address is covered; Xmpp library at 100/100
