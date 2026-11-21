---
id: BL-1520
title: Decide whether a delay fault's hold ends when the client closes or resets its connection
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-20
completed:
---
# BL-1520 — Decide whether a delay fault's hold ends when the client closes or resets its connection

## Goal

An amendment to ADR-0141, decided by Claude under Stewart's delegation, says whether and how a `delay` rule's hold (at `Accept` and at a command) ends early when the client closes or resets the connection, so a cancelled request stops counting against `--max-connections` and `--max-connections-per-address`.

## Context

Surly cookbook bug report 2026-11-20, item 3 (Surl `9ad60d80c`; unchanged on `factory/phase-1` at `299737d24`): a client that cancels a request held by a `delay` fault keeps its slot until the delay ends, so with `MaxConnectionsPerAddress = 1` the next request is refused `503`. The delay is at most 600000 ms (ADR-0124 decision 4), so the slot is held for up to ten minutes after the client has gone.

Why, in today's code:
- `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:817` (`ApplyAcceptDelayOrFindDropAsync`) awaits `Task.Delay(delay, timeProvider, context.CancellationToken)`. That token is `ExchangeDeadlines.Token` (`ExchangeDeadlines.cs:32-41`): shutdown, idle timeout and maximum duration only, and the idle clock is paused for the hold (`deadlines.PauseIdleClock()`, line 816). Nothing reads the connection during the hold, so a FIN or reset from the client is never seen.
- The slot is released only in `RunExchangeAsync`'s `finally` (`ServingEngine.cs:739`, `route.Admission.Release`), after the hold and the serve have run.
- The command-stage hold is `Foundation/Surl.Protocol.Abstractions.UnitLibrary/CommandStageFaults.cs:81` (`HoldAsync`), the same `Task.Delay` on the exchange's token. HTTP calls the overload without a connection (`Protocols/Http/Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs:38`), because an HTTP/2 or HTTP/3 stream shares its connection.

What the decision must settle:
1. Whether a hold ends when the client leaves (the stand-in rule says a test must get its slot back; the delay's meaning is "the server is slow", and a slow real server notices a reset at its next write, not before).
2. How a peer close is seen without losing bytes: `IConnection.PeekByteAsync` returns `null` both for "nothing yet" and for "half-closed with nothing unread", and returns the first byte, not the close, when the request already arrived (curl sends it at once). The options include reading ahead into a bounded buffer that the server then reads first (and that a TLS handshake after an `Accept` hold must receive, as ADR-0113 decision 6 already does for one peeked byte), or a new default-implemented `IConnection` member; a contract change in `Surl.Protocol.Abstractions` is its own task, landing before the engine task.
3. What is logged and published when a hold ends early (a note such as `Exchange <id>: the client closed the connection during the hold by fault rule <id>.`; whether `ExchangeEndedEvent` differs).
4. Which servers' command-stage holds change, if any, and in which tasks.

Stand-in: no store; a test observes the early end on the event stream (`ExchangeEndedEvent`) and by being admitted again under a one-connection limit.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md` has a dated amendment, marked "Decided by Claude under Stewart's delegation", answering points 1 to 4 above for the `Accept` and `Command` stages.
- [ ] The amendment names the type and member that sees the client leave, the project it lives in, and how no byte the client sent before leaving is lost to a server that is still served.
- [ ] The amendment states the exchange-log note's text and what is published when a hold ends early.
- [ ] Every implementation task the amendment needs beyond BL-1521 (a contract member, a `Surl.Networking` change, each command-stage server family) is filed, and BL-1521's `depends-on` lists the ones it needs.
- [ ] `Documentation/Planning/Decisions/README.md`'s ADR-0141 row mentions the amendment.

## Notes

## Log

- 2026-11-20: Created.
