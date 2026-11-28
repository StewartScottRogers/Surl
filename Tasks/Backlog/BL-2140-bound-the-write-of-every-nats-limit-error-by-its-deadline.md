---
id: BL-2140
title: Bound the write of every NATS limit error by its deadline
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2138]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: NFR-017
created: 2026-11-28
completed:
---
# BL-2140 — Bound the write of every NATS limit error by its deadline

## Goal

Every `-ERR` the `nats` and `natss` server sends before closing for a limit is given up on at its deadline (one second under ADR-0006 section 5; the slow consumer's as BL-2138 decides), and the connection then closes gracefully. This task changes nothing in `Documentation/Product/Requirements.md`; BL-2143 re-states NFR-017.

## Context

Found while working BL-2120 (2026-11-28). In `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsServerConnection.cs`, a `NatsProtocolViolationException` (`Maximum Control Line Exceeded`, `Maximum Payload Violation`, `Authentication Timeout`, the resource limit and the other violations) is enqueued on the outbox and written under `context.CancellationToken`; `AnswerTlsTimeoutAsync` writes the same way. A peer that stops reading holds the connection until the idle timeout. The deadline runs on `context.TimeProvider` from the violation and covers the bytes queued ahead of the `-ERR`, the `-ERR` and the completion of writes; applying it to every violation is the simplest form. The slow consumer's path (`CutOffSlowConsumerAsync`, `NatsServerOptions.SlowConsumerErrorTimeout`) takes the number the BL-2138 amendment of ADR-0006 states. The connection-limit refusal is already bounded by `ServingEngine`. Stand-in: a test with a stalled client sees the exchange end at the deadline; nothing new to seed, reset or publish.

## Acceptance criteria

- [ ] A test with a connection whose write never completes shows a payload violation ends the exchange once one second has passed on a fake `TimeProvider`, and not before, without aborting the connection.
- [ ] A test shows the same for the `Authentication Timeout` answer, on both the plain and the TLS-wait path.
- [ ] A test pins the slow consumer's deadline at the value the amendment states.
- [ ] The existing violation tests still send the same bytes and close as before.
- [ ] `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests -warnaserror` is clean and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
