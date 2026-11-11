---
id: BL-782
title: Trace the AMQP 1.0 matrix rows BL-513 owns to their named tests
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-513]
touches: [Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-782 — Trace the AMQP 1.0 matrix rows BL-513 owns to their named tests

## Goal

Every row of `Documentation/Conformance/Extended/Amqp1.md` whose evidence column still reads `BL-513`
names the test in `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests` that shows it, as
ADR-0107's Consequences ask ("each task replaces its ID with the named test").

## Context

- BL-513 built transfers, credit, settlement and the message format but could not edit the matrix:
  `Documentation/Conformance` was in the `touches` of BL-394, in Doing at the time.
- The tests are in `Amqp1ContainerConnectionTests` (its `.Transfers.cs` and `.Settlement.cs` parts),
  `Amqp1BusMessagesTests`, `Amqp1BusNodeDirectoryTests`, `Amqp1DeliveryStateTests` and
  `Amqp1IncomingDeliveryTests`.
- ADR-0107's "Amendment: how BL-513 reads decisions 5 and 6" narrows two rows: a queue `reply-to` has
  no bus field (Surl-Message-1), and settling with no outcome releases a queue delivery.
- `Amqp1:Surl-Storage-1` stays `n/a` (persistence is the bus's).

## Acceptance criteria

- [ ] `Select-String -Path Documentation/Conformance/Extended/Amqp1.md -Pattern 'BL-513'` finds no row
      whose evidence column is only `BL-513`; each names one or more tests as `Class.Method`.
- [ ] Each named test exists (`dotnet test --list-tests` on the Amqp1 test project lists it).
- [ ] The Surl-Message-1 row states that a `reply-to` naming a queue travels only in the native form.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
