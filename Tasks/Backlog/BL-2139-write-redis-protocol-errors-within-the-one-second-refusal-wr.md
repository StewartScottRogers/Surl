---
id: BL-2139
title: Write Redis protocol errors within the one-second refusal write deadline
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: NFR-017
created: 2026-11-28
completed:
---
# BL-2139 — Write Redis protocol errors within the one-second refusal write deadline

## Goal

The `redis` and `rediss` server gives up on a protocol error reply, those for a limit included, one second after it starts writing it, and then closes gracefully (ADR-0006 section 5). This task changes nothing in `Documentation/Product/Requirements.md`; BL-2143 re-states NFR-017.

## Context

Found while working BL-2120 (2026-11-28). In `Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary/RedisServerConnection.cs`, `AnswerAsync`'s `ProtocolError` case (`too big inline request`, `invalid bulk length` and the other replies of ADR-0106 decision 6) calls `RedisReplyWriter.WriteAsync` and `CompleteWritesAsync` under the writer's exchange-long token, so a peer that stops reading holds the connection until the idle timeout. The one second runs on `context.TimeProvider` and covers the wait for the writer's gate (a published message may be mid-write), the write and the half-close. When it passes, the server notes it in the exchange log and ends without aborting. `CommandTooLarge` writes nothing and the connection-limit refusal is already bounded by `ServingEngine`. Stand-in: a test with a stalled client sees the exchange end after one fake second; nothing new to seed, reset or publish.

## Acceptance criteria

- [ ] A test with a connection whose write never completes shows a too-long inline line ends the exchange once one second has passed on a fake `TimeProvider`, and not before.
- [ ] A test shows the same when the write gate is held by a delivery that never completes.
- [ ] Neither case aborts the connection, and the exchange log notes that the error was not written in time.
- [ ] The existing protocol error tests still send the same bytes and half-close.
- [ ] `dotnet build Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests -warnaserror` is clean and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
