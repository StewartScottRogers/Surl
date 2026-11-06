---
id: BL-811
title: Register event callbacks and bounded response overrides as hooks on a Surl.Api server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-810]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-811 — Register event callbacks and bounded response overrides as hooks on a Surl.Api server

## Goal

In-process test code registers a callback that runs on chosen events, and a response override that decides, per
matching request, which fault (delay, drop, refuse login, chosen protocol error, out of space) the server applies
- and nothing a hook does can loosen a secure default, hang the server or crash it.

## Context

- Decision: BL-822's ADR decision 5. Planning surface: `SurlServer.OnEvent(query, callback)` returning an
  `IDisposable` registration; `SurlServer.Faults.AddOverride(scope, decide)` where `decide` returns a `SurlFault`
  or null per matching request. The override is a fault rule whose answer is computed, so it reaches servers
  through the same fault plan and `ExchangeContext.Faults` (BL-792), and protocol servers need no change.
- Bounds the ADR fixes and the tests prove: a callback runs off the serving path (an event callback never blocks a
  server; a slow one loses events as any slow subscriber does); an override runs with a time limit, past which the
  request is served as if no hook were registered and an event says so; an exception from a hook is caught,
  published as an event, and the request is served as if no hook were registered; an override can only choose what
  a fault rule can - it cannot accept a refused login, turn TLS off, reach outside the served root, or widen an
  option.
- Hooks are `Surl.Api` only (in-process); the control endpoint and `Surl.Mcp` offer fault rules, not callbacks.
- Where: `Application/Surl.Api`. Update the package `README.md` with a "hooks" section including the bounds.
- Stand-in: subscriber of the event stream plus a computed fault rule; the extension point for test-specific
  behaviour.
- Amended by ADR-0124 decision 5: `OnEvent(SurlEventQuery, Func<SurlEvent, CancellationToken, ValueTask>)`;
  `AddOverride(SurlFaultScope, Func<FaultPoint, CancellationToken, ValueTask<SurlFault?>>)`; `HookTimeLimit`
  default 1 second (1 ms to 30 seconds) on the injected `TimeProvider`; failures publish `HookFailedEvent`
  (`Threw` or `TimedOut`) and the hook stays registered.

## Acceptance criteria

- [x] Named tests (fake listeners and connections, fake `TimeProvider`): an event callback runs for each matching
      event and stops after its registration is disposed; an override that returns a protocol error for path `/a`
      makes `GET /a` fail and `GET /b` succeed; an override past its time limit and an override that throws each
      leave the request served normally and publish the ADR's event; a callback that throws does not stop
      serving.
- [x] A named test shows no override can make a wrong password log in or a `.surl` path be served.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

- **Touches widened to `Foundation/Surl.Core.UnitLibrary` and `Surl.Core.UnitTests`.** ADR-0124 decision 5 has an
  override "sit in the fault plan's order like any rule", and servers get the concrete `FaultPlan` straight from the
  composition, so `Surl.Api` cannot wrap it from outside. `FaultPlan` gained `AddOverride(FaultOverrideScope,
  FaultOverride)` and a public `StageOf`. No task in Doing named Surl.Core (Composition, held by BL-816, was not
  needed).
- Tests: `SurlServerTests.Hooks.cs` (`OnEvent_ACallback_RunsForEachMatchingEventAndStopsOnceItsRegistrationIsDisposed`,
  `OnEvent_ACallbackThatThrows_PublishesHookFailedAndServingGoesOn`,
  `AddOverride_AProtocolErrorForPathA_MakesGetAFailAndGetBSucceed`,
  `AddOverride_PastItsTimeLimit_ServesTheRequestNormallyAndPublishesTimedOut`,
  `AddOverride_ThatThrows_ServesTheRequestNormallyPublishesThrewAndStaysAdded`,
  `AddOverride_AnsweringEveryFaultAtEveryPoint_NeverLogsInAWrongPasswordNorServesADotSurlPath`),
  `ResponseOverrideHookTests`, `EventCallbackHookTests`, `FaultPlanTests.Overrides.cs`. Coverage measured with
  `Measure-CodeQuality.ps1 -SkipTestRun`: Surl.Api and Surl.Core.UnitLibrary at 100% line and branch, worst CRAP 10.
- Decisions (Claude, under Stewart's delegation, inside ADR-0124 decision 5; BL-969 records them in the ADR, which
  BL-707 held during this run):
  - An override's answer applies to the point it was asked at: its scope and `Times` are dropped, and an `error`
    takes the point's scheme, so the reply is checked against that protocol's error shape by `surl`'s reader.
  - An answer `surl` refuses, or whose kind cannot act at the point's stage, is a hook failure (`Threw`, the refusal
    text) and the point is served as if no override were there - `HookFailure` has only `Threw` and `TimedOut`.
  - An override's hook ID is its rule ID `f<n>` (what `Remove` takes); a callback's is `h<n>` per server.
  - A `SurlFaultScope` without `Stage` is asked at every stage; listener and scheme together are refused, as
    `--fault` refuses them.
  - The decision runs on `Task.Factory.StartNew` (not `Task.Run`, which would turn a thrown
    `OperationCanceledException` into a cancellation and lose its message), so even a decision that blocks before its
    first await is bounded. Its token's source is released only once a late decision ends, since it may register on
    the token after the time limit.
  - A callback that throws on its own `HookFailedEvent` is not published again (no feedback loop); a callback ended
    by disposing its registration is not a failure. Callbacks start no more once disposed, buffered events included,
    and the server disposes every callback on stop.
  - Overrides count toward the 256 rules and are not listed in `FaultPlan.Rules` (they have no kind until asked).

## Log

- 2026-11-05: Created.
- 2026-11-05: Amended by ADR-0124 (BL-822): async override, 1 s default time limit, `HookFailedEvent`.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Surl.Api servers take event callbacks (OnEvent) and bounded response overrides (Faults.AddOverride, HookTimeLimit) that publish HookFailedEvent and never loosen a default
