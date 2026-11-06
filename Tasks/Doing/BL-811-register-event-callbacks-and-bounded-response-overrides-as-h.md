---
id: BL-811
title: Register event callbacks and bounded response overrides as hooks on a Surl.Api server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-810]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-05
completed:
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

- [ ] Named tests (fake listeners and connections, fake `TimeProvider`): an event callback runs for each matching
      event and stops after its registration is disposed; an override that returns a protocol error for path `/a`
      makes `GET /a` fail and `GET /b` succeed; an override past its time limit and an override that throws each
      leave the request served normally and publish the ADR's event; a callback that throws does not stop
      serving.
- [ ] A named test shows no override can make a wrong password log in or a `.surl` path be served.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Amended by ADR-0124 (BL-822): async override, 1 s default time limit, `HookFailedEvent`.
- 2026-11-06: Backlog -> Doing.
