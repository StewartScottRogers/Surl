---
id: BL-2141
title: Bound the write of every STOMP limit error by its deadline
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2138]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests]
requirement: NFR-017
created: 2026-11-28
completed:
---
# BL-2141 — Bound the write of every STOMP limit error by its deadline

## Goal

Every `ERROR` frame the `stomp` and `stomps` broker sends before closing for a limit is given up on at its deadline (one second under ADR-0006 section 5; the slow consumer's as BL-2138 decides), and the connection then closes gracefully. This task changes nothing in `Documentation/Product/Requirements.md`; BL-2143 re-states NFR-017.

## Context

Found while working BL-2120 (2026-11-28). In `Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary/StompServerConnection.cs`, a `StompProtocolViolationException` (`Header line too long`, `Frame too large`, too many headers, `Resource limit exceeded` and the other violations) is written directly under `context.CancellationToken` while connecting, and enqueued on the outbox afterwards, which is written under the same token. A peer that stops reading holds the connection until the idle timeout. The deadline runs on `context.TimeProvider` from the violation and covers the bytes queued ahead of the `ERROR`, the `ERROR` and the completion of writes; applying it to every violation is the simplest form. The slow consumer's path (`CutOffSlowConsumerAsync`, `StompServerOptions.SlowConsumerErrorTimeout`) takes the number the BL-2138 amendment of ADR-0006 states. `Linger` stays. The connection-limit refusal is already bounded by `ServingEngine`. Stand-in: a test with a stalled client sees the exchange end at the deadline; nothing new to seed, reset or publish.

## Acceptance criteria

- [ ] A test with a connection whose write never completes shows a violation before `CONNECTED` ends the exchange once one second has passed on a fake `TimeProvider`, and not before, without aborting the connection.
- [ ] A test shows the same for a `Frame too large` after `CONNECTED`.
- [ ] A test pins the slow consumer's deadline at the value the amendment states.
- [ ] The existing violation tests still send the same bytes and close as before.
- [ ] `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests -warnaserror` is clean and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
