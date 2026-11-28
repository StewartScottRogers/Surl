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
completed: 2026-11-28
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

- [x] A test in `Surl.Extended.Protocol.Nats.UnitTests` makes the abort land while a write is in progress, without relying on thread timing, and passes.
- [x] `ServeAsync_ADropRuleForSub_AbortsTheConnectionWithNoReply` passes, and still asserts the abort, no `PONG` and the one `FaultAppliedEvent`.
- [x] The project's `CLAUDE.md` states how `ServeAsync` ends after a `drop` rule.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for the NATS test project.

## Notes

- Decision: after a `drop` rule `ServeAsync` returns quietly. ADR-0141 decision 2 already says the server sends
  nothing more and ends the session, and the other servers (MQTT, MQTT 5, LDAP, the mail servers) end that way,
  so no new ADR.
- Cause: `CommandStageFaults` aborts the connection before the NATS server abandons its outbox, so the writer
  could meet the abort first and `ServeAsync` rethrew its `IOException`. The write failure is now ignored once a
  drop rule applied, which holds whichever side runs first. A failed write with no drop is still thrown.
- Also fixed, same cause: an operation already buffered behind the dropped one (a `PUB` after the dropped `SUB`)
  was still served. The read loop now stops at the drop.
- `ServeAsync_ADropRuleWhileAWriteIsInProgress_EndsWithoutThrowing` holds the `+OK` write until the abort fails
  it (`UnitTestNatsClientConnection.FailsHeldWritesOnAbort`). It and
  `ServeAsync_ADropRule_ServesNoOperationThatArrivedBehindTheDroppedOne` both failed before the fix.
- Verified 2026-11-28: `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean, 30059 fast
  tests passed (3 skipped), NATS 226 of 226 ten runs in a row, `Measure-CodeQuality.ps1` 0 failing members for the
  NATS library.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. A NATS drop rule ends ServeAsync quietly however the aborted write ends, and serves nothing buffered behind the dropped operation.
