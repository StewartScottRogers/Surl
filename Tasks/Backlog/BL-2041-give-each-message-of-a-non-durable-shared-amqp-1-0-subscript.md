---
id: BL-2041
title: Give each message of a non-durable shared AMQP 1.0 subscription to one of its links
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2040]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2041 — Give each message of a non-durable shared AMQP 1.0 subscription to one of its links

## Goal

Two links attached to one `shared` source with `durable` `none` receive each published message once between them.

## Context

- Found while working BL-1990 (2026-11-27) by reading the code; prove it with a failing test first.
- ADR-0146 decision 3.5, as BL-2040's dated amendment applies it to non-durable sources. Read the amendment first
  and do exactly that.
- Today `Amqp1KeptSubscription.IsAskedFor` leaves such a source out and
  `Amqp1ContainerConnection.Links.cs`, `Subscribe`, gives each link its own live subscription.
  `Amqp1Terminus` already reads `IsShared` and `IsGlobal`.
- Shared tests to build beside:
  `Amqp1ContainerConnectionTests.Subscriptions.cs`. Matrix row `Amqp1:Surl-Subscriptions-2` in
  `Documentation/Conformance/Extended/Amqp1.md`.
- Stand-in: the shared message bus, so a message another protocol publishes is shared out the same way. No new
  event or fault.

## Acceptance criteria

- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: two links on one non-durable shared source receive each message exactly once between them, and the echoed source carries the applied `shared` (and `global`) capability; the test failed before the change (or Notes say why this is no defect).
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: after one link detaches the other receives every later message, and after both detach nothing of the subscription remains on the bus.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: a non-durable shared attach leaves a durable subscription of the same link name and its messages in place, and an unshared non-durable source still gets every message on each link.
- [ ] The library's `CLAUDE.md` states the rule, and row `Amqp1:Surl-Subscriptions-2` cites the new tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
