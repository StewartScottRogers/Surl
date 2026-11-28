---
id: BL-2115
title: Make the NATS drop-rule test for SUB pass whichever way the aborted write ends
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2115 — Make the NATS drop-rule test for SUB pass whichever way the aborted write ends

## Goal

`NatsFaultTests.ServeAsync_ADropRuleForSub_AbortsTheConnectionWithNoReply` passes on every run, however the
outbox writer and the connection abort are ordered.

## Context

Seen while working BL-2108 on 2026-11-28, dark factory lane 1, Windows: the full fast run failed this one test, and
three runs of `Surl.Extended.Protocol.Nats.UnitTests` alone then passed (224 of 224 each). BL-2108 changed no NATS
file.

The failure was `System.IO.IOException: The connection was aborted.` thrown out of `NatsServer.ServeAsync`, from
`InMemoryConnection.WriteAsync` under `NatsOutbox.WriteAllAsync` and `NatsServerConnection.WriteThenStopAsync`
(`NatsServerConnection.cs`). Reading of the stack: the writer was still writing queued bytes when the `drop` rule
aborted the connection. `ServeAsync` rethrows that write failure. The project's `CLAUDE.md` says a `drop` rule aborts
the connection, `NatsOutbox.Abandon` discards everything queued and reading stops (ADR-0141). Decide from ADR-0141 and
the other servers' drop handling whether a drop should end `ServeAsync` quietly or let the `IOException` out, and
make the server and its test agree.

Stand-in (root `CLAUDE.md`, "Surl is a stand-in for tests"): a test that injects a `drop` fault must get the same
outcome every run.

## Acceptance criteria

- [ ] A test in `Surl.Extended.Protocol.Nats.UnitTests` makes the abort land while a write is in progress, without relying on thread timing, and passes.
- [ ] `ServeAsync_ADropRuleForSub_AbortsTheConnectionWithNoReply` passes, and still asserts the abort, no `PONG` and the one `FaultAppliedEvent`.
- [ ] The project's `CLAUDE.md` states how `ServeAsync` ends after a `drop` rule.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for the NATS test project.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
