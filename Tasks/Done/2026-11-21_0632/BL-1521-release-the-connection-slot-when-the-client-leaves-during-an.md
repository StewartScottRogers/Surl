---
id: BL-1521
title: Release the connection slot when the client leaves during an Accept-stage delay per BL-1520's ADR-0141 amendment
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1520, BL-1529, BL-1530]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-21
---
# BL-1521 — Release the connection slot when the client leaves during an Accept-stage delay per BL-1520's ADR-0141 amendment

## Goal

A client that closes or resets its connection while the serving engine holds it under an `Accept`-stage `delay` rule ends the exchange then, not when the delay runs out, so its `--max-connections` and `--max-connections-per-address` slot is free for the next client.

## Context

Surly cookbook bug report 2026-11-20, item 3. Read BL-1520's amendment to `Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md` first: it decides how the client's leaving is seen and what is logged and published. If it filed a contract or `Surl.Networking` task, that lands first (it is in this task's `depends-on`); this task is the engine half.

The code:
- `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs:801-821` (`ApplyAcceptDelayOrFindDropAsync`): the hold is `await Task.Delay(delay, timeProvider, context.CancellationToken)` at line 817, on `ExchangeDeadlines.Token`, which a peer close never cancels.
- `ServingEngine.cs:722-741` (`RunExchangeAsync`): `route.Admission.Release(remoteEndPoint)` at line 739 runs only after the hold and the serve.
- `ConnectionAdmission.cs` counts the slots; `ServingEngine.cs:656` is where a new connection is refused.

The hold must still keep every byte the client sent for a client that stays (the server reads the request as before), keep the idle clock paused and restarted as now, and end at shutdown as now. Command-stage holds are BL-1520's other tasks, not this one.

Stand-in: no store; a test sees the early end as the `ExchangeEndedEvent` on the instance's `SurlEventStream` and by a second connection from the same address being admitted under `MaxConnectionsPerAddress = 1` before the clock reaches the delay.

## Acceptance criteria

- [x] A `ServingEngineTests.Faults.cs` test: under `delay;ms=60000` and a one-connection-per-address limit, a client that half-closes during the hold ends its exchange without the `ManualTimeProvider` advancing, and a second connection from the same address is served, not refused.
- [x] The same for a client whose connection is reset (its read throws `IOException`) during the hold.
- [x] A test that a client which sends its request and stays connected is served after the delay with every byte it sent, as `ServeAsync_DelayRule_HoldsTheExchangeUntilTheClockAdvancesByTheDelay_AndPublishesIt` shows today.
- [x] A test that the exchange log holds the note BL-1520's amendment names when the hold ends early.
- [x] `dotnet build Foundation/Surl.Core.UnitLibrary -warnaserror` is clean and `dotnet test Foundation/Surl.Core.UnitTests --filter "TestCategory!=Integration"` passes, with 100% line and branch coverage of the changed files; no test needs `TestCategory=Integration`.
- [x] Core's own `IConnection` wrappers (`IdleClockRestartingConnection`, `RecordingConnection`, `DroppingAfterBytesConnection`) forward `WatchForPeerCloseAsync`; the idle-clock wrapper does not restart the idle clock for it (ADR-0141 Amendment 2 decision 2).
- [x] `Foundation/Surl.Core.UnitLibrary/CLAUDE.md` states that an `Accept`-stage hold ends when the client leaves.

## Notes

Carried over from lane 2's branch (cherry-picked). The two Core tests and the DnsQuery TCP delay test that failed there now pass on a fresh rebase; full fast suite green on this lane (build clean, 0 failures).

## Log

- 2026-11-20: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Lane 2 could not integrate: fast tests failed twice (Surl.Core.UnitTests: ServeAsync_ClientResetsDuringAnAcceptDelay_AbortsTheExchangeAtOnce_AndFreesItsSlot, ServeAsync_ClientHalfClosesDuringAnAcceptDelay_EndsTheExchangeAtOnce_AndFreesItsSlot; then Surl.Extended.Protocol.DnsQuery.UnitTests: ServeAsync_TcpQueryUnderADelayRule_IsAnsweredOnlyAfterTheDelay) after rebasing onto the other lanes' work. The work is on branch factory/BL-1521-lane-2-20261121-011657; start with git cherry-pick --no-commit factory/BL-1521-lane-2-20261121-011657 and fix it.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. An Accept-stage delay hold ends when the client half-closes or resets, freeing its connection slot
