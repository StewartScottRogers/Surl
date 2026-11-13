---
id: BL-1110
title: Make the WebSocket limit-close test wait for the lingering close before advancing the fake clock
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-1110 — Make the WebSocket limit-close test wait for the lingering close before advancing the fake clock

## Goal

`WsClosingTests.ExchangeCancelledForALimit_IsClosedWith1001_ThenLingered` passes on CI's
Windows, Linux and macOS jobs every run, and an exchange cancelled for a limit still closes
with 1001 and ends after its lingering close.

## Context

CI run 43506096520 (PR #33, frozen branch `factory/merge-20261113-123056`, commit e6077e2): the
test's `("idle timeout")` row failed only on "Build and test (ubuntu-latest)" after 30 s.
`WsServerHarness.EndLingeringCloseAsync` threw `System.TimeoutException`, called from
`WsClosingTests.cs:77`. It passed on Windows and macOS. That holds the merge of #33 to
`master`.

- Test: `Protocols/Http/Surl.Protocol.Ws.UnitTests/WsClosingTests.cs`, the `[DataRow]` test
  `ExchangeCancelledForALimit_IsClosedWith1001_ThenLingered`.
- Harness: `Protocols/Http/Surl.Protocol.Ws.UnitTests/WsServerHarness.cs:137`.
  `EndLingeringCloseAsync` calls `WaitForLingeringCloseAsync`, then advances the
  `ManualTimeProvider` by `WebSocketLingeringClose.MaxLingerTime`, then waits
  `ExchangeEndTimeout`. A timeout means the serving task never ended. Either the advance
  happened before the lingering-close timer was registered, or `WaitForLingeringCloseAsync`
  returned too early.
- Production: `WebSocketLingeringClose` and the limit-close path in
  `Protocols/Http/Surl.Protocol.Ws.UnitLibrary`.

## Acceptance criteria

- [ ] The task's Log names the cause: either the harness advances the clock before the lingering-close timer exists, or production never registers or never fires it after a limit cancel.
- [ ] `WaitForLingeringCloseAsync` returns only once the lingering close's timer is registered with the fake clock, shown by a test that fails without the fix where the race can be forced deterministically, and it uses no `Task.Delay`, `Thread.Sleep` or real time.
- [ ] Every other `WsClosingTests` test using `EndLingeringCloseAsync` still passes unchanged.
- [ ] The test passes 50 runs in a row on Windows: `dotnet test Protocols/Http/Surl.Protocol.Ws.UnitTests --filter "FullyQualifiedName~WsClosingTests"` looped 50 times.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green. Surl.Protocol.Ws.UnitLibrary keeps 100% line and branch coverage.

## Notes

Filed by an interactive session on 2026-11-13 after PR #33's Linux job failed. Lanes test only on
Windows, where this passes, so reason about the race rather than rely on reproducing it.
BL-1109 is the same kind of race in the HTTP head-timeout tests.

## Log

- 2026-11-13: Created.
